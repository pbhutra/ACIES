using Microsoft.SharePoint.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using Newtonsoft.Json;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace CustomWorkflows
{
    public class CreateSharepointFolders : CodeActivity
    {
        [Input("Property Owners Application")]
        [ReferenceTarget("lux_propertyownersapplications")]
        public InArgument<EntityReference> PropertyOwnersApplication { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            tracingService.Trace("Application Started");

            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            EntityReference applnref = PropertyOwnersApplication.Get<EntityReference>(executionContext);
            Entity appln = new Entity(applnref.LogicalName, applnref.Id);
            appln = service.Retrieve("lux_propertyownersapplications", applnref.Id, new ColumnSet(true));

            Entity appln1 = service.Retrieve("lux_propertyownersapplications", applnref.Id, new ColumnSet(false));

            using (ClientContext clientContext = new ClientContext("https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint"))
            {
                string PolicyFolderName = appln.Attributes["lux_name"].ToString().Trim().Replace(".", "-").Replace("&", " - ").Replace("%", " - ").Replace(":", " - ").Replace("#", " - ").Replace("<", " - ").Replace(">", " - ").Replace("|", " - ").Replace("/", " - ").Replace("\"", " - ") + "_" + appln.Id.ToString().Replace("-", "") + "_Policy Documents";
                string BrokerFolderName = appln.Attributes["lux_name"].ToString().Trim().Replace(".", "-").Replace("&", " - ").Replace("%", " - ").Replace(":", " - ").Replace("#", " - ").Replace("<", " - ").Replace(">", " - ").Replace("|", " - ").Replace("/", " - ").Replace("\"", " - ") + "_" + appln.Id.ToString().Replace("-", "") + "_Broker Documents";

                SecureString passWord = new SecureString();
                foreach (char c in "Nup496791".ToCharArray()) passWord.AppendChar(c);
                clientContext.Credentials = new SharePointOnlineCredentials("lucidux@aciesmgu.com", passWord);

                string FetchXML = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='true' >
                                            <entity name='sharepointdocumentlocation' >
                                                <attribute name='sharepointdocumentlocationid' />
                                                <filter type='and' >
                                                    <condition attribute='relativeurl' operator='eq' value='lux_propertyownersapplications' />
                                                </filter>
                                            </entity>
                                        </fetch>";

                EntityCollection result = service.RetrieveMultiple(new FetchExpression(FetchXML));

                string FetchXML1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='sharepointdocumentlocation'>
                                            <attribute name='name' />
                                            <attribute name='regardingobjectid' />
                                            <attribute name='parentsiteorlocation' />
                                            <attribute name='relativeurl' />
                                            <attribute name='absoluteurl' />
                                            <attribute name='locationtype' />
                                            <attribute name='description' />
                                            <attribute name='createdon' />
                                            <order attribute='name' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='servicetype' operator='eq' value='0' />
                                            </filter>
                                            <link-entity name='lux_propertyownersapplications' from='lux_propertyownersapplicationsid' to='regardingobjectid' link-type='inner' alias='aa'>
                                              <filter type='and'>
                                                <condition attribute='lux_propertyownersapplicationsid' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                              </filter>
                                            </link-entity>
                                          </entity>
                                        </fetch>";

                EntityCollection result1 = service.RetrieveMultiple(new FetchExpression(FetchXML1));

                if (result1.Entities.Count() == 0)
                {
                    Entity sharepointdocumentlocation1 = new Entity("sharepointdocumentlocation");
                    sharepointdocumentlocation1["name"] = "Policy Documents";
                    sharepointdocumentlocation1["parentsiteorlocation"] = result[0].ToEntityReference();
                    sharepointdocumentlocation1["relativeurl"] = PolicyFolderName;
                    sharepointdocumentlocation1["regardingobjectid"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                    Guid sharepointdocumentlocationid1 = service.Create(sharepointdocumentlocation1);

                    Entity sharepointdocumentlocation = new Entity("sharepointdocumentlocation");
                    sharepointdocumentlocation["name"] = "Broker Documents";
                    sharepointdocumentlocation["parentsiteorlocation"] = result[0].ToEntityReference();
                    sharepointdocumentlocation["relativeurl"] = BrokerFolderName;
                    sharepointdocumentlocation["regardingobjectid"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                    Guid sharepointdocumentlocationid = service.Create(sharepointdocumentlocation);

                    appln1.Attributes["lux_sharepointpolicyfoldername"] = "https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint/lux_propertyownersapplications/" + PolicyFolderName;
                    appln1.Attributes["lux_sharepointbrokerfoldername"] = "https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint/lux_propertyownersapplications/" + BrokerFolderName;
                    service.Update(appln1);
                }
                else if (result1.Entities.Count() == 1)
                {
                    Entity sharepointdocumentlocation1 = new Entity("sharepointdocumentlocation", result1.Entities[0].Id);
                    sharepointdocumentlocation1["name"] = "Policy Documents";
                    sharepointdocumentlocation1["relativeurl"] = PolicyFolderName;
                    service.Update(sharepointdocumentlocation1);

                    Entity sharepointdocumentlocation = new Entity("sharepointdocumentlocation");
                    sharepointdocumentlocation["name"] = "Broker Documents";
                    sharepointdocumentlocation["parentsiteorlocation"] = result[0].ToEntityReference();
                    sharepointdocumentlocation["relativeurl"] = BrokerFolderName;
                    sharepointdocumentlocation["regardingobjectid"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                    Guid sharepointdocumentlocationid = service.Create(sharepointdocumentlocation);

                    appln1.Attributes["lux_sharepointpolicyfoldername"] = "https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint/lux_propertyownersapplications/" + PolicyFolderName;
                    appln1.Attributes["lux_sharepointbrokerfoldername"] = "https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint/lux_propertyownersapplications/" + BrokerFolderName;
                    service.Update(appln1);
                }

                //var dictForApiParams = new Dictionary<string, string>();
                //dictForApiParams.Add("Application", appln.Id.ToString());

                //var data = new StringContent(JsonConvert.SerializeObject(new
                //{
                //    Application = appln.Id.ToString()
                //}));

                var client = new HttpClient();
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                /*For Live*/client.BaseAddress = new Uri("https://bc67079b6f3e4321944af366558303.53.environment.api.powerplatform.com:443/powerautomate/automations/direct/workflows/2aa2d68158c24dfaa1e888ed3c4bb0cd/triggers/manual/paths/invoke?api-version=1&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=NCU6Br89LsvlHswSydp59b2Vc45NCnzES51aTHIj8B0");
               // /*For UAT*/client.BaseAddress = new Uri("https://prod-20.uksouth.logic.azure.com:443/workflows/4b947a08d1b740b9a3faf845bc58fc8a/triggers/manual/paths/invoke?api-version=2016-06-01&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=KCW-G_Yy0clGMz63VfLulv2k8F2CB9Jq7gPqOP6PtWQ");
                var apiRequest = new HttpRequestMessage(HttpMethod.Post, client.BaseAddress) { Content = new StringContent("{\r\n  \"Application\": \"" + appln.Id.ToString() + "\"}", System.Text.Encoding.UTF8, "application/json") };

                //var apiRequest = new HttpRequestMessage(HttpMethod.Post, client.BaseAddress) { Content = data };
                var apiResponseString = ProcessWebResponse(client, apiRequest, tracingService);
                var apiResponse = apiResponseString.Result;

                tracingService.Trace(apiResponse);

                //try
                //{
                //    var targetFolder = clientContext.Web.GetFolderByServerRelativeUrl("https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint/lux_propertyownersapplications/" + BrokerFolderName);
                //    var uploadFile = targetFolder;
                //    clientContext.Load(uploadFile);
                //    clientContext.ExecuteQuery();
                //}
                //catch (Exception ex)
                //{
                //    var targetFolder1 = clientContext.Web.GetFolderByServerRelativeUrl("https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint/lux_propertyownersapplications/");
                //    var newTargetFolder = targetFolder1.Folders.Add(BrokerFolderName);
                //    var uploadFile1 = newTargetFolder;
                //    clientContext.Load(uploadFile1);
                //    clientContext.ExecuteQuery();
                //}
                //try
                //{
                //    var targetFolder2 = clientContext.Web.GetFolderByServerRelativeUrl("https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint/lux_propertyownersapplications/" + PolicyFolderName);
                //    var uploadFile2 = targetFolder2;
                //    clientContext.Load(uploadFile2);
                //    clientContext.ExecuteQuery();
                //}
                //catch (Exception ex)
                //{
                //    var targetFolder2 = clientContext.Web.GetFolderByServerRelativeUrl("https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint/lux_propertyownersapplications/");
                //    var newTargetFolder2 = targetFolder2.Folders.Add(PolicyFolderName);
                //    var uploadFile2 = newTargetFolder2;
                //    clientContext.Load(uploadFile2);
                //    clientContext.ExecuteQuery();
                //}
            }
        }

        public static async Task<string> ProcessWebResponse(HttpClient client, HttpRequestMessage apiRequest, ITracingService tracingService)
        {
            var reponseContentString = "";
            try
            {
                HttpResponseMessage apiResponse = await client.SendAsync(apiRequest);
                reponseContentString = await apiResponse.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                tracingService.Trace(ex.Message + Environment.NewLine + ex.StackTrace);
            }
            tracingService.Trace(reponseContentString);
            return reponseContentString;
        }
    }
}