using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using System.Xml;
using System.Xml.Serialization;

namespace BlackBarLabs.Security.SessionServer.Api.Controllers
{
    public class ContentNegotiationController : Controller
    {
        public const string JavaScriptObjectNotation = "application/json";
        public const string TextXml = "text/xml";
        internal class XmlResult : ActionResult
        {
            public object Data { get; set; }


            public override void ExecuteResult(ControllerContext context)
            {
                //if (Data != null)
                //{
                //    var xs = new XmlSerializer(Data.GetType());
                //    context.HttpContext.Response.ContentType = "text/xml";
                //    xs.Serialize(context.HttpContext.Response.Output, Data);
                //}

                var response = context.HttpContext.Response;
                response.ContentType = TextXml;

                var writer = XmlWriter.Create(response.Output, new XmlWriterSettings() { OmitXmlDeclaration = true });

                new XmlSerializer(Data.GetType()).Serialize(writer, Data, new XmlSerializerNamespaces(new[] { new XmlQualifiedName("", "") }));
            }
        }

        public class AutoContentNegotiation : ActionFilterAttribute
        {
            private readonly String[] _actionParams;
           
            //Deserialze
            public AutoContentNegotiation(params String[] parameters)
            {
                this._actionParams = parameters;
            }

            public override void OnActionExecuted(ActionExecutedContext filterContext)
            {
                if (!(filterContext.Result is ViewResult)) return;

                // Setup
                var utf8 = new UTF8Encoding(false);
                var request = filterContext.RequestContext.HttpContext.Request;
                var acceptTypes = request.AcceptTypes ?? new string[] {};
                var view = (ViewResult)(filterContext.Result);
                var data = view.ViewData.Model;

                var model = filterContext.Controller.ViewData.Model;

                if (acceptTypes.Any(x => x == JavaScriptObjectNotation) || request.IsAjaxRequest())
                {
                    filterContext.Result = new JsonResult() { Data = model, JsonRequestBehavior = JsonRequestBehavior.AllowGet };
                    filterContext.RequestContext.HttpContext.Response.ContentType = JavaScriptObjectNotation;
                }

                else if (acceptTypes.Any(x => x == TextXml))
                {
                    filterContext.Result = new XmlResult() { Data = model };
                    filterContext.RequestContext.HttpContext.Response.ContentType = TextXml;
                }
            }

            // DESERIALIZE
            public override void OnActionExecuting(ActionExecutingContext filterContext)
            {

                if (_actionParams == null || _actionParams.Length == 0) return;

                var request = filterContext.RequestContext.HttpContext.Request;
                var acceptTypes = request.AcceptTypes ?? new string[] { };
                var isJson = acceptTypes.Any(x => x == JavaScriptObjectNotation);

                if (!isJson) return;
                //@@todo Deserialize POX

                // JavascriptSerialier expects a single type to deserialize
                // so if the response contains multiple disparate objects to deserialize
                // we dynamically build a new wrapper class with fields representing those
                // object types, deserialize and then unwrap
                var paramDescriptors =
                        filterContext.ActionDescriptor.GetParameters();
                var complexType = paramDescriptors.Length > 1;

                Type wrapperClass;
                if (complexType)
                {
                    var parameterInfo = new Dictionary<string, Type>();
                    foreach (ParameterDescriptor p in paramDescriptors)
                    {
                        parameterInfo.Add(p.ParameterName, p.ParameterType);
                    }
                    wrapperClass = BuildWrapperClass(parameterInfo);
                }
                else
                {
                    wrapperClass = paramDescriptors[0].ParameterType;
                }

                String json;
                using (var sr = new StreamReader(request.InputStream))
                {
                    json = sr.ReadToEnd();
                }

                // then deserialize json as instance of dynamically created wrapper class
                var serializer = new JavaScriptSerializer();
                var result = typeof(JavaScriptSerializer)
                                .GetMethod("Deserialize")
                                .MakeGenericMethod(wrapperClass)
                                .Invoke(serializer, new object[] { json });

                // then get fields from wrapper class assign the values back to the action params
                if (complexType)
                {
                    for (var i = 0; i < paramDescriptors.Length; i++)
                    {
                        var pd = paramDescriptors[i];
                        filterContext.ActionParameters[pd.ParameterName] =
                                wrapperClass.GetField(pd.ParameterName).GetValue(result);

                    }
                }
                else
                {
                    var pd = paramDescriptors[0];
                    filterContext.ActionParameters[pd.ParameterName] = result;
                }
            }

            private Type BuildWrapperClass(Dictionary<string, Type> parameterInfo)
            {
                var assemblyName = new AssemblyName();
                assemblyName.Name = "DynamicAssembly";
                var appDomain = AppDomain.CurrentDomain;
                var assemblyBuilder =
                        appDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
                var moduleBuilder =
                        assemblyBuilder.DefineDynamicModule("DynamicModule");
                var typeBuilder =
                        moduleBuilder.DefineType("DynamicClass",
                        TypeAttributes.Public | TypeAttributes.Class);

                foreach (KeyValuePair<String, Type> entry in parameterInfo)
                {
                    var paramName = entry.Key;
                    var paramType = entry.Value;
                    var field = typeBuilder.DefineField(paramName,
                                            paramType, FieldAttributes.Public);
                }

                var generatedType = typeBuilder.CreateType();
                // object generatedObject = Activator.CreateInstance(generatedType);

                return generatedType;
            }

        }
    }
}