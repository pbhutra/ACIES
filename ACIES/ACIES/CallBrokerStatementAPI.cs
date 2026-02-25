using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ACIES
{
    public class CallBrokerStatementAPI : CodeActivity
    {
        [RequiredArgument]
        [Input("Email to Update")]
        [ReferenceTarget("email")]
        public InArgument<EntityReference> Email { get; set; }

        [RequiredArgument]
        [Input("Broker")]
        [ReferenceTarget("account")]
        public InArgument<EntityReference> Broker { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            tracingService.Trace("Application Started");

            //Create the context
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            Dictionary<string, string> dictForApiParams = new Dictionary<string, string>();
            dictForApiParams.Add("Email", Email.Get(executionContext).Id.ToString());
            dictForApiParams.Add("Guid", Broker.Get(executionContext).Id.ToString());

            foreach (var item in dictForApiParams)
            {
                tracingService.Trace(item.Key + " - " + item.Value);
            }

            var url = "https://apidataverse.azurewebsites.net/api/TransactionExcel";
            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.BaseAddress = new Uri(url.Trim());
            var request = new HttpRequestMessage(HttpMethod.Post, client.BaseAddress) { Content = new FormUrlEncodedContent(dictForApiParams) };
            var responseString = ProcessWebResponse(client, request, tracingService);
            var response = responseString.Result;
            tracingService.Trace("Response: " + response);
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
