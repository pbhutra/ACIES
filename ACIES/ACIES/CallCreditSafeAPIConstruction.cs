using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace ACIES
{
    public class CallCreditSafeAPIConstruction : CodeActivity
    {
        [Input("Contact")]
        [ReferenceTarget("contact")]
        public InArgument<EntityReference> Contact { get; set; }

        [Input("Construction Quote")]
        [ReferenceTarget("lux_constructionquotes")]
        public InArgument<EntityReference> ConstructionQuote { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            tracingService.Trace("Application Started");

            //Create the context
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            EntityReference applnref = ConstructionQuote.Get<EntityReference>(executionContext);
            Entity appln = new Entity(applnref.LogicalName, applnref.Id);
            appln = service.Retrieve("lux_constructionquotes", applnref.Id, new ColumnSet(true));

            EntityReference conref = Contact.Get<EntityReference>(executionContext);
            Entity con = new Entity(conref.LogicalName, conref.Id);
            con = service.Retrieve("contact", conref.Id, new ColumnSet(true));

            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            /*For Live*/
            client.BaseAddress = new Uri("https://bc67079b6f3e4321944af366558303.53.environment.api.powerplatform.com:443/powerautomate/automations/direct/workflows/1e64a5efa2764d2998ee31811e076939/triggers/manual/paths/invoke?api-version=1&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=UhEcJi_BEWhjxzrDySEFNfORzJ7EHDM3Btnv1P-sxdw");

            ///*For UAT*/
            //client.BaseAddress = new Uri("https://prod-17.uksouth.logic.azure.com:443/workflows/a72aa3f03ab045b08c4fe88dc0c89b1d/triggers/manual/paths/invoke?api-version=2016-06-01&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=C-9PKFvS7oiIqi9UVOgFy2jewICHYl7520zyMSqA0TM");

            var apiRequest = new HttpRequestMessage(HttpMethod.Post, client.BaseAddress) { Content = new StringContent("{\r\n  \"Construction_Quote_ID\": \"" + appln.Id.ToString() + "\", \r\n  \"Contact_ID\": \"" + con.Id.ToString() + "\"}", System.Text.Encoding.UTF8, "application/json") };
            var apiResponseString = ProcessWebResponse(client, apiRequest, tracingService);
            var apiResponse = apiResponseString.Result;

            tracingService.Trace(apiResponse);
        }

        public static async Task<string> ProcessWebResponse(HttpClient client, HttpRequestMessage request, ITracingService tracingService)
        {
            var reponseContentString = "";
            try
            {
                HttpResponseMessage response = await client.SendAsync(request);
                tracingService.Trace(response.ToString());
                reponseContentString = await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                tracingService.Trace(ex.Message + Environment.NewLine + ex.InnerException + Environment.NewLine + ex.StackTrace);
            }
            return reponseContentString;
        }
    }
}
