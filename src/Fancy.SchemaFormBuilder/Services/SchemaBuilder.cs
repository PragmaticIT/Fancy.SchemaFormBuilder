using System;
using System.Collections.Generic;
using System.Reflection;
using Fancy.SchemaFormBuilder.Annotations;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace Fancy.SchemaFormBuilder.Services
{
    /// <summary>
    /// Implements the <see cref="ISchemaBuilder"/> interface.
    /// </summary>
    public class SchemaBuilder : ISchemaBuilder
    {
        /// <summary>
        /// The pipeline modules.
        /// </summary>
        private List<ISchemaBuilderModule> _pipelineModules;

        /// <summary>
        /// Initializes a new instance of the <see cref="SchemaBuilder"/> class.
        /// </summary>
        public SchemaBuilder()
        {
            _pipelineModules = new List<ISchemaBuilderModule>();
        }

        /// <summary>
        /// Builds the JSON schema for a specified type.
        /// </summary>
        /// <param name="type">The type to build the schema for.</param>
        /// <param name="cultureInfo"></param>
        /// <returns>
        /// The JSON schema.
        /// </returns>
        public JObject BuildSchema(Type type, CultureInfo cultureInfo)
        {
            return BuildSchema(type, type, cultureInfo);
        }

        /// <summary>
        /// Builds the JSON schema for a specified type.
        /// </summary>
        /// <param name="type">The type to build the schema for.</param>
        /// <param name="originType">Type of the origin object for which the processing was started.</param
        /// <param name="cultureInfo">The culture information.</param>
        /// <returns>
        /// The JSON schema.
        /// </returns>
        public JObject BuildSchema(Type type, Type originType, CultureInfo cultureInfo)
        {
            PropertyInfo[] propertyInfos = type.GetProperties();
            
            JObject schemaElements = new JObject();
            JArray requiredSchemaElements = new JArray();

            // Run through each property of the type
            foreach (PropertyInfo propertyInfo in propertyInfos)
            {
                SchemaElement element = ProcessProperty(type, originType, propertyInfo, cultureInfo);

                if (element.Schema != null)
                {
                    // The the schema element was build add it to the result schema elements
                    schemaElements[propertyInfo.Name] = (element.Schema);

                    if (element.IsRequired)
                    {
                        // If the property is required add it to the list of required properties
                        requiredSchemaElements.Add(new JValue(propertyInfo.Name));
                    }
                }
            }

            // After processing all properties build the schema object
            JObject schema = new JObject();
            schema["type"] = new JValue("object");
            schema["properties"] = schemaElements;
            schema["required"] = requiredSchemaElements;

            // If the type has a title add is
            FormTitleAttribute titleAttribute = type.GetTypeInfo().GetCustomAttribute<FormTitleAttribute>();

            if (titleAttribute != null)
            {
                schema["title"] = new JValue(titleAttribute.Title);
            }

            return schema;
        }

        /// <summary>
        /// Adds a module to the pipeline.
        /// </summary>
        /// <param name="module">The module.</param>
        public void AddPipelineModule(ISchemaBuilderModule module)
        {
            _pipelineModules.Add(module);
        }

        /// <summary>
        /// Processes a property through the pipeline modules.
        /// </summary>
        /// <param name="objectType">Type of the object beeing processed.</param>
        /// <param name="originObjectType">Type of the origin object.</param>
        /// <param name="propertyInfo">The property information.</param>
        /// <param name="cultureInfo">The culture information.</param>
        /// <returns>
        /// The schema element.
        /// </returns>
        private SchemaElement ProcessProperty(Type objectType, Type originObjectType, PropertyInfo propertyInfo, CultureInfo cultureInfo)
        {
            // Create the context for this property
            SchemaBuilderContext context = new SchemaBuilderContext();
            context.Property = propertyInfo;
            context.SchemaBuilder = this;
            context.TargetCulture = cultureInfo;
            context.DtoType = objectType;
            context.OriginDtoType = originObjectType;

            // Run the property through each pipeline module
            foreach (ISchemaBuilderModule builderModule in _pipelineModules)
            {
                builderModule.Process(context);

                if (context.FinishProcessing)
                {
                    // Stop the pipeline to run the next module
                    break;
                }
            }

            return context.Element;
        }
    }
}