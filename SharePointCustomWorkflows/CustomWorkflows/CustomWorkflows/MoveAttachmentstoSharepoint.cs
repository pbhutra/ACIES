using Microsoft.SharePoint.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Threading.Tasks;

namespace CustomWorkflows
{
    public class MoveAttachmentstoSharepoint : CodeActivity
    {
        [Input("Property Owners Application")]
        [ReferenceTarget("lux_propertyownersapplications")]
        public InArgument<EntityReference> PropertyOwnersApplication { get; set; }

        [Input("Tradesman Application")]
        [ReferenceTarget("lux_tradesman")]
        public InArgument<EntityReference> TradesmanApplication { get; set; }

        [Input("Note")]
        [ReferenceTarget("annotation")]
        public InArgument<EntityReference> Note { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            tracingService.Trace("Application Started");

            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            if (TradesmanApplication.Get(executionContext) == null)
            {
                EntityReference applnref = PropertyOwnersApplication.Get<EntityReference>(executionContext);
                Entity appln = new Entity(applnref.LogicalName, applnref.Id);
                appln = service.Retrieve("lux_propertyownersapplications", applnref.Id, new ColumnSet(true));

                EntityReference noteref = Note.Get<EntityReference>(executionContext);
                Entity note = new Entity(noteref.LogicalName, noteref.Id);
                note = service.Retrieve("annotation", noteref.Id, new ColumnSet(true));

                var BrokerFolderName = appln.Attributes["lux_sharepointbrokerfoldername"].ToString();
                if (BrokerFolderName == "")
                    BrokerFolderName = appln.Attributes["lux_name"].ToString().Replace(".", "-").Replace("&", " - ").Replace("%", " - ").Replace(":", " - ").Replace("#", " - ").Replace("<", " - ").Replace(">", " - ").Replace("|", " - ").Replace("/", " - ").Replace("\"", " - ") + "_" + appln.Id.ToString().Replace("-", "") + "_Broker Documents";

                var notesFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                  <entity name='annotation'>
                                    <attribute name='subject' />
                                    <attribute name='notetext' />
                                    <attribute name='filename' />
                                    <attribute name='documentbody' />
                                    <attribute name='annotationid' />
                                    <order attribute='subject' descending='false' />
                                      <filter type='and'>
                                         <condition attribute='annotationid' operator='eq' uiname='' uitype='annotation' value='{note.Id}' />
                                      </filter>
                                   </entity>
                                </fetch>";

                var file = service.RetrieveMultiple(new FetchExpression(notesFetch)).Entities;
                if (file.Count() > 0)
                {

                    var client = new HttpClient();
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.BaseAddress = new Uri("https://bc67079b6f3e4321944af366558303.53.environment.api.powerplatform.com:443/powerautomate/automations/direct/workflows/e12a95c460254a17a1672cdf17fadce4/triggers/manual/paths/invoke?api-version=1&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=0BL2re6-tXX715ZfEMQpIy3uxPasYZS7FuygqndO-jQ");

                    var apiRequest = new HttpRequestMessage(HttpMethod.Post, client.BaseAddress) { Content = new StringContent("{\r\n  \"Note\": \"" + note.Id.ToString() + "\", \r\n  \"Entity\": \"lux_propertyownersapplications\"}", System.Text.Encoding.UTF8, "application/json") };

                    //var apiRequest = new HttpRequestMessage(HttpMethod.Post, client.BaseAddress) { Content = data };
                    var apiResponseString = ProcessWebResponse(client, apiRequest, tracingService);
                    var apiResponse = apiResponseString.Result;

                    tracingService.Trace(apiResponse);





                    //using (ClientContext clientContext = new ClientContext("https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint"))
                    //{
                    //    SecureString passWord = new SecureString();
                    //    foreach (char c in "Nup496791".ToCharArray()) passWord.AppendChar(c);
                    //    clientContext.Credentials = new SharePointOnlineCredentials("lucidux@aciesmgu.com", passWord);

                    //    foreach (var item in file)
                    //    {
                    //        FileCreationInformation fcInfo = new FileCreationInformation();
                    //        fcInfo.Overwrite = true;
                    //        fcInfo.Url = item.Attributes["filename"].ToString();
                    //        //fcInfo.Content = System.Convert.FromBase64String(item.Attributes["documentbody"].ToString());
                    //        fcInfo.ContentStream = new MemoryStream(System.Convert.FromBase64String(item.Attributes["documentbody"].ToString()));

                    //        BrokerFolderName = BrokerFolderName.Replace("https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint/lux_propertyownersapplications/", "");

                    //        try
                    //        {
                    //            var targetFolder = clientContext.Web.GetFolderByServerRelativeUrl("https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint/lux_propertyownersapplications/" + BrokerFolderName);
                    //            var uploadFile = targetFolder.Files.Add(fcInfo);
                    //            clientContext.Load(uploadFile);
                    //            clientContext.ExecuteQuery();
                    //        }
                    //        catch (Exception ex)
                    //        {
                    //            var targetFolder1 = clientContext.Web.GetFolderByServerRelativeUrl("https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint/lux_propertyownersapplications/");
                    //            var newTargetFolder = targetFolder1.Folders.Add(BrokerFolderName);
                    //            var uploadFile1 = newTargetFolder.Files.Add(fcInfo);
                    //            clientContext.Load(uploadFile1);
                    //            clientContext.ExecuteQuery();
                    //        }
                    //        service.Delete("annotation", item.Id);
                    //    }
                    //}
                }
            }
            else
            {
                tracingService.Trace(TradesmanApplication.Get<EntityReference>(executionContext).Id.ToString());
                EntityReference applnref = TradesmanApplication.Get<EntityReference>(executionContext);
                Entity appln = new Entity(applnref.LogicalName, applnref.Id);
                appln = service.Retrieve("lux_tradesman", applnref.Id, new ColumnSet(true));

                EntityReference noteref = Note.Get<EntityReference>(executionContext);
                Entity note = new Entity(noteref.LogicalName, noteref.Id);
                note = service.Retrieve("annotation", noteref.Id, new ColumnSet(true));

                var BrokerFolderName = appln.Attributes["lux_sharepointbrokerfoldername"].ToString();
                if (BrokerFolderName == "")
                    BrokerFolderName = "https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint/lux_tradesman/" + appln.Attributes["lux_insuredtitle"].ToString().Trim().Replace(".", "-").Replace("&", " - ").Replace("%", " - ").Replace(":", " - ").Replace("#", " - ").Replace("<", " - ").Replace(">", " - ").Replace("|", " - ").Replace("/", " - ").Replace("\"", " - ") + "_" + appln.Id.ToString().Replace("-", "") + "_Broker Documents";

                tracingService.Trace(BrokerFolderName);

                var notesFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                  <entity name='annotation'>
                                    <attribute name='subject' />
                                    <attribute name='notetext' />
                                    <attribute name='filename' />  
                                    <attribute name='documentbody' />
                                    <attribute name='annotationid' />
                                    <order attribute='subject' descending='false' />
                                      <filter type='and'>
                                         <condition attribute='annotationid' operator='eq' uiname='' uitype='annotation' value='{note.Id}' />
                                      </filter>
                                  </entity>
                                </fetch>";

                var file = service.RetrieveMultiple(new FetchExpression(notesFetch)).Entities;
                if (file.Count() > 0)
                {
                    var client = new HttpClient();
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.BaseAddress = new Uri("https://bc67079b6f3e4321944af366558303.53.environment.api.powerplatform.com:443/powerautomate/automations/direct/workflows/e12a95c460254a17a1672cdf17fadce4/triggers/manual/paths/invoke?api-version=1&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=0BL2re6-tXX715ZfEMQpIy3uxPasYZS7FuygqndO-jQ");

                    var apiRequest = new HttpRequestMessage(HttpMethod.Post, client.BaseAddress) { Content = new StringContent("{\r\n  \"Note\": \"" + note.Id.ToString() + "\", \r\n  \"Entity\": \"lux_tradesman\"}", System.Text.Encoding.UTF8, "application/json") };

                    //var apiRequest = new HttpRequestMessage(HttpMethod.Post, client.BaseAddress) { Content = data };
                    var apiResponseString = ProcessWebResponse(client, apiRequest, tracingService);
                    var apiResponse = apiResponseString.Result;

                    tracingService.Trace(apiResponse);

                    //using (ClientContext clientContext = new ClientContext("https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint"))
                    //{
                    //    SecureString passWord = new SecureString();
                    //    foreach (char c in "Nup496791".ToCharArray()) passWord.AppendChar(c);
                    //    clientContext.Credentials = new SharePointOnlineCredentials("lucidux@aciesmgu.com", passWord);

                    //    foreach (var item in file)
                    //    {
                    //        FileCreationInformation fcInfo = new FileCreationInformation();
                    //        fcInfo.Overwrite = true;
                    //        fcInfo.Url = item.Attributes["filename"].ToString();
                    //        //fcInfo.Content = System.Convert.FromBase64String(item.Attributes["documentbody"].ToString());
                    //        fcInfo.ContentStream = new MemoryStream(System.Convert.FromBase64String(item.Attributes["documentbody"].ToString()));

                    //        BrokerFolderName = BrokerFolderName.Replace("https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint/lux_tradesman/", "");

                    //        try
                    //        {
                    //            tracingService.Trace("1 " + item.Id.ToString());
                    //            var targetFolder = clientContext.Web.GetFolderByServerRelativeUrl("https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint/lux_tradesman/" + BrokerFolderName);
                    //            var uploadFile = targetFolder.Files.Add(fcInfo);
                    //            clientContext.Load(uploadFile);
                    //            clientContext.ExecuteQuery();

                    //            tracingService.Trace("2 " + item.Id.ToString());
                    //        }
                    //        catch (Exception ex)
                    //        {
                    //            var targetFolder1 = clientContext.Web.GetFolderByServerRelativeUrl("https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint/lux_tradesman/");
                    //            var newTargetFolder = targetFolder1.Folders.Add(BrokerFolderName);
                    //            var uploadFile1 = newTargetFolder.Files.Add(fcInfo);
                    //            clientContext.Load(uploadFile1);
                    //            clientContext.ExecuteQuery();
                    //        }
                    //        service.Delete("annotation", item.Id);
                    //    }
                    //}
                }
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