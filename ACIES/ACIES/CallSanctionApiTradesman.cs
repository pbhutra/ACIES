using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System.Activities;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;
using System;
using System.Net.Http.Headers;
//using RestSharp;
//using Newtonsoft.Json.Linq;

namespace ACIES
{
    public class CallSanctionApiTradesman : CodeActivity
    {
        [RequiredArgument]
        [Input("SearchTerm")]
        public InArgument<string> SearchTerm { get; set; }

        [RequiredArgument]
        [Input("APIKey")]
        public InArgument<string> APIKey { get; set; }

        [RequiredArgument]
        [Input("Tradesman")]
        [ReferenceTarget("lux_tradesman")]
        public InArgument<EntityReference> Tradesman { get; set; }

        [RequiredArgument]
        [Input("Sanction")]
        [ReferenceTarget("lux_sanction")]
        public InArgument<EntityReference> Sanction { get; set; }

        [RequiredArgument]
        [Input("SharePointUrl")]
        public InArgument<string> SharePointUrl { get; set; }

        [RequiredArgument]
        [Input("SharePointId")]
        public InArgument<string> SharePointId { get; set; }

        [RequiredArgument]
        [Input("SharePointPwd")]
        public InArgument<string> SharePointPwd { get; set; }

        [RequiredArgument]
        [Input("AciesUrl")]
        public InArgument<string> AciesUrl { get; set; }

        [RequiredArgument]
        [Input("ClientId")]
        public InArgument<string> ClientId { get; set; }

        [RequiredArgument]
        [Input("ClientSecret")]
        public InArgument<string> ClientSecret { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            tracingService.Trace("Application Started");

            //Create the context
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            Dictionary<string, string> dictForApiParams = new Dictionary<string, string>();
            dictForApiParams.Add("APIKey", APIKey.Get(executionContext).ToString());
            dictForApiParams.Add("SearchTerm", SearchTerm.Get(executionContext).ToString());
            dictForApiParams.Add("ApplicationId", Tradesman.Get(executionContext).Id.ToString());
            dictForApiParams.Add("SanctionId", Sanction.Get(executionContext).Id.ToString());
            dictForApiParams.Add("SharePointUrl", SharePointUrl.Get(executionContext).ToString());
            dictForApiParams.Add("SharePointId", SharePointId.Get(executionContext).ToString());
            dictForApiParams.Add("SharePointPwd", SharePointPwd.Get(executionContext).ToString());
            dictForApiParams.Add("AciesUrl", AciesUrl.Get(executionContext).ToString());
            dictForApiParams.Add("ClientId", ClientId.Get(executionContext).ToString());
            dictForApiParams.Add("ClientSecret", ClientSecret.Get(executionContext).ToString());

            foreach (var item in dictForApiParams)
            {
                tracingService.Trace(item.Key + " - " + item.Value);
            }

            var url = "https://msdynamicswebapi.azurewebsites.net/api/ACIES/CallSanctionApi";
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
