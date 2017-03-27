﻿using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.IO;
using System.Text.Encodings.Web;
using System.Linq;
namespace DynamicFormTagHelper.TagHelpers
{
    public static class FormGroupBuilder
    {
        public static Task<string> GetFormGroup(ModelExplorer property, IHtmlGenerator generator, ViewContext viewContext, HtmlEncoder encoder)
        {
            if (IsSimpleType(property.ModelType))
            {
                return _getFormGroupForSimpleProperty(property, generator, viewContext, encoder);
            }
            else
            {
                return _getFormGroupsForComplexProperty(property, generator, viewContext, encoder);
            }
        }

        private static async Task<string> _getFormGroupForSimpleProperty(ModelExplorer property, IHtmlGenerator generator, ViewContext viewContext, HtmlEncoder encoder)
        {
            string label = await buildLabelHtml(generator, property, viewContext, encoder);
            string input = await buildInputHtml(generator, property, viewContext, encoder);
            string validation = await buildValidationMessage(generator, property, viewContext, encoder);
            return $@"<div class='form-group'>
                {label}
                {input}
                {validation}
</div>";
        }

        private static async Task<string> _getFormGroupsForComplexProperty(ModelExplorer property, IHtmlGenerator generator, ViewContext viewContext, HtmlEncoder encoder)
        {
            StringBuilder builder = new StringBuilder();

            string label = await buildLabelHtml(generator, property, viewContext, encoder);
            foreach (var prop in property.Properties)
            {
                builder.Append(await _getFormGroupForSimpleProperty(prop, generator, viewContext, encoder));
            }

            return $@"<div class='form-group'>
                    {label}
                    <div class=""sub-form-group"">
                        {builder.ToString()}
                    </div>
</div>";
        }
        
        private static bool IsSimpleType(Type propertyType)
        {
            Type[] simpleTypes = new Type[]
            {
                typeof(string), typeof(bool), typeof(byte), typeof(char), typeof(DateTime), typeof(DateTimeOffset),
                typeof(decimal), typeof(double), typeof(Guid), typeof(short), typeof(int), typeof(long), typeof(float),
                typeof(TimeSpan), typeof(ushort), typeof(uint),typeof(ulong)
            };
            if (propertyType.IsConstructedGenericType && propertyType.Name.Equals("Nullable`1"))
            {
                return IsSimpleType(propertyType.GenericTypeArguments.First());
            }
            else
            {
                return (!propertyType.IsArray && !propertyType.IsPointer && simpleTypes.Contains(propertyType));
            }
        }

        private static async Task<string> buildLabelHtml(IHtmlGenerator generator, ModelExplorer property, ViewContext viewContext, HtmlEncoder encoder)
        {
            TagHelper label = new LabelTagHelper(generator)
            {
                For = new ModelExpression(getFullPropertyName(property), property),
                ViewContext = viewContext
            };
            return await GetGeneratedContent("label", TagMode.StartTagAndEndTag, label, encoder: encoder);
        }

        private static async Task<string> buildInputHtml(IHtmlGenerator generator, ModelExplorer property, ViewContext viewContext, HtmlEncoder encoder)
        {
           TagHelper input = new InputTagHelper(generator)
           {
                For = new ModelExpression(getFullPropertyName(property), property),
                ViewContext = viewContext
            };

            return await GetGeneratedContent("input",
                TagMode.SelfClosing,
                input,
                attributes: new TagHelperAttributeList { new TagHelperAttribute("class", "form-control")
                },
                encoder: encoder
                );
        }
        private static string getFullPropertyName(ModelExplorer property)
        {
            List<string> nameComponents = new List<String> { property.Metadata.PropertyName };

            while (!string.IsNullOrEmpty(property.Container.Metadata.PropertyName))
            {
                nameComponents.Add(property.Container.Metadata.PropertyName);
                property = property.Container;
            }

            nameComponents.Reverse();
            return string.Join(".", nameComponents);
        }
        private static async Task<string> buildValidationMessage(IHtmlGenerator generator, ModelExplorer property, ViewContext viewContext, HtmlEncoder encoder)
        {
            TagHelper validationMessage = new ValidationMessageTagHelper(generator)
            {
                For = new ModelExpression(getFullPropertyName(property), property),
                ViewContext = viewContext
            };
            return await GetGeneratedContent("span", TagMode.StartTagAndEndTag, validationMessage, encoder: encoder);
        }

        private static async Task<string> GetGeneratedContent(string tagName, TagMode tagMode,
            ITagHelper tagHelper, HtmlEncoder encoder, TagHelperAttributeList attributes = null )
        {
            if (attributes == null)
            {
                attributes = new TagHelperAttributeList();
            }

            TagHelperOutput output = new TagHelperOutput(tagName, attributes, (arg1, arg2) =>
            {
                return Task.Run<TagHelperContent>(() => new DefaultTagHelperContent());
            })
            {
                TagMode = tagMode
            };
            TagHelperContext context = new TagHelperContext(attributes, new Dictionary<object, object>(), Guid.NewGuid().ToString());
            
            await tagHelper.ProcessAsync(context, output);

            return output.RenderTag(encoder);
        }

        private static string RenderTag(this TagHelperOutput output, HtmlEncoder encoder)
        {
            using (var writer = new StringWriter())
            {
                output.WriteTo(writer, encoder);
                return writer.ToString();
            }
        }
    }
}
