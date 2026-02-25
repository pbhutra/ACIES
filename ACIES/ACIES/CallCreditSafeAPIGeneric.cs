using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace ACIES
{
    public class CallCreditSafeAPIGeneric : CodeActivity
    {
        [RequiredArgument]
        [Input("ContactRecordURL")]
        public InArgument<string> ContactRecordURL { get; set; }

        [RequiredArgument]
        [Input("RecordURL")]
        public InArgument<string> RecordURL { get; set; }

        [RequiredArgument]
        [Input("API Url")]
        public InArgument<string> APIUrl { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            tracingService.Trace("Application Started");

            //Create the context
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            
            String _recordURL = this.ContactRecordURL.Get(executionContext);
            string[] urlParts = _recordURL.Split("?".ToArray());
            string[] urlParams = urlParts[1].Split("&".ToCharArray());
            string PrimaryObjectTypeCode = urlParams[0].Replace("etc=", "");
            string PrimaryentityName = sGetEntityNameFromCode(PrimaryObjectTypeCode, service);
            string PrimaryId = urlParams[1].Replace("id=", "");

            String _recordURL1 = this.RecordURL.Get(executionContext);
            string[] urlParts1 = _recordURL1.Split("?".ToArray());
            string[] urlParams1 = urlParts1[1].Split("&".ToCharArray());
            string PrimaryObjectTypeCode1 = urlParams1[0].Replace("etc=", "");
            string PrimaryentityName1 = sGetEntityNameFromCode(PrimaryObjectTypeCode1, service);
            string PrimaryId1 = urlParams1[1].Replace("id=", "");

            var url = this.APIUrl.Get(executionContext).ToString();
            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.BaseAddress = new Uri(url.Trim());

            var apiRequest = new HttpRequestMessage(HttpMethod.Post, client.BaseAddress) { Content = new StringContent("{\r\n  \"Quote_ID\": \"" + PrimaryId1.ToString() + "\", \r\n  \"Contact_ID\": \"" + PrimaryId.ToString() + "\"}", System.Text.Encoding.UTF8, "application/json") };
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

        public string sGetEntityNameFromCode(string ObjectTypeCode, IOrganizationService service)
        {
            MetadataFilterExpression entityFilter = new MetadataFilterExpression(LogicalOperator.And);
            entityFilter.Conditions.Add(new MetadataConditionExpression("ObjectTypeCode", MetadataConditionOperator.Equals, Convert.ToInt32(ObjectTypeCode)));
            EntityQueryExpression entityQueryExpression = new EntityQueryExpression()
            {
                Criteria = entityFilter
            };
            RetrieveMetadataChangesRequest retrieveMetadataChangesRequest = new RetrieveMetadataChangesRequest()
            {
                Query = entityQueryExpression,
                ClientVersionStamp = null
            };
            RetrieveMetadataChangesResponse response = (RetrieveMetadataChangesResponse)service.Execute(retrieveMetadataChangesRequest);

            EntityMetadata entityMetadata = (EntityMetadata)response.EntityMetadata[0];
            return entityMetadata.SchemaName.ToLower();
        }
    }
}
