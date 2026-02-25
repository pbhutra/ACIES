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
    public class CallCreditSafeApi : CodeActivity
    {
        [RequiredArgument]
        [Input("APIUserName")]
        public InArgument<string> APIUserName { get; set; }

        [RequiredArgument]
        [Input("APIPassword")]
        public InArgument<string> APIPassword { get; set; }

        [RequiredArgument]
        [Input("Countries")]
        public InArgument<string> Countries { get; set; }

        [RequiredArgument]
        [Input("CompanyName")]
        public InArgument<string> CompanyName { get; set; }

        [RequiredArgument]
        [Input("RegNo")]
        public InArgument<string> RegNo { get; set; }

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

        [RequiredArgument]
        [Input("Application")]
        [ReferenceTarget("lux_propertyownersapplications")]
        public InArgument<EntityReference> Application { get; set; }

        [Output("CreditScore")]
        public OutArgument<string> CreditScore { get; set; }

        [Output("CreditRating")]
        public OutArgument<string> CreditRating { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            tracingService.Trace("Application Started");

            //Create the context
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            Dictionary<string, string> dictForApiParams = new Dictionary<string, string>();
            dictForApiParams.Add("APIUserName", APIUserName.Get(executionContext));
            dictForApiParams.Add("APIPassword", APIPassword.Get(executionContext));
            dictForApiParams.Add("Countries", Countries.Get(executionContext));
            dictForApiParams.Add("CompanyName", CompanyName.Get(executionContext));
            dictForApiParams.Add("RegNo", RegNo.Get(executionContext));
            dictForApiParams.Add("SharePointUrl", SharePointUrl.Get(executionContext).ToString());
            dictForApiParams.Add("SharePointId", SharePointId.Get(executionContext).ToString());
            dictForApiParams.Add("SharePointPwd", SharePointPwd.Get(executionContext).ToString());
            dictForApiParams.Add("AciesUrl", AciesUrl.Get(executionContext).ToString());
            dictForApiParams.Add("ClientId", ClientId.Get(executionContext).ToString());
            dictForApiParams.Add("ClientSecret", ClientSecret.Get(executionContext).ToString());
            dictForApiParams.Add("ApplicationId", Application.Get(executionContext).Id.ToString());

            foreach (var item in dictForApiParams)
            {
                tracingService.Trace(item.Key + " - " + item.Value);
            }

            var url = "https://msdynamicswebapi.azurewebsites.net/api/ACIES/CallCreditSafeApi";
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
