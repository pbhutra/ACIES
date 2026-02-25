using ACIES.Model;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ACIES
{
    public class CallAragApi : CodeActivity
    {
        [RequiredArgument]
        [Input("Acies Url")]
        public InArgument<string> AciesUrl { get; set; }

        [RequiredArgument]
        [Input("Client Id")]
        public InArgument<string> ClientId { get; set; }

        [RequiredArgument]
        [Input("Client Secret")]
        public InArgument<string> ClientSecret { get; set; }

        [RequiredArgument]
        [Input("Arag Sftp Host Address")]
        public InArgument<string> AragSftpHostAddress { get; set; }

        [RequiredArgument]
        [Input("Arag Sftp Port")]
        public InArgument<int> AragSftpPort { get; set; }

        [RequiredArgument]
        [Input("Arag Username")]
        public InArgument<string> AragUserName { get; set; }

        [RequiredArgument]
        [Input("Arag Password")]
        public InArgument<string> AragPassword { get; set; }

        [RequiredArgument]
        [Input("Arag Upload Folder")]
        public InArgument<string> AragUploadFolder { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            tracingService.Trace("Application Started");

            //Create the context
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            AragApiModel aragApiModel = new AragApiModel();

            aragApiModel.AciesUrl = AciesUrl.Get(executionContext).ToString();
            aragApiModel.ClientId = ClientId.Get(executionContext).ToString();
            aragApiModel.ClientSecret = ClientSecret.Get(executionContext).ToString();
            aragApiModel.AragSftpHostAddress = AragSftpHostAddress.Get(executionContext).ToString();
            aragApiModel.AragSftpPort = AragSftpPort.Get(executionContext);
            aragApiModel.AragUserName = AragUserName.Get(executionContext).ToString();
            aragApiModel.AragPassword = AragPassword.Get(executionContext).ToString();
            aragApiModel.AragUploadFolder = AragUploadFolder.Get(executionContext).ToString();

            //var jsonstring = JsonConvert.SerializeObject(aragApiModel);
            //tracingService.Trace(jsonstring);
            Dictionary<string, string> keyValuePairs = new Dictionary<string, string>();
            keyValuePairs.Add("AciesUrl", aragApiModel.AciesUrl);
            keyValuePairs.Add("ClientId", aragApiModel.ClientId);
            keyValuePairs.Add("ClientSecret", aragApiModel.ClientSecret);
            keyValuePairs.Add("AragSftpHostAddress", aragApiModel.AragSftpHostAddress);
            keyValuePairs.Add("AragSftpPort", aragApiModel.AragSftpPort.ToString());
            keyValuePairs.Add("AragUserName", aragApiModel.AragUserName);
            keyValuePairs.Add("AragPassword",aragApiModel.AragPassword);
            keyValuePairs.Add("AragUploadFolder", aragApiModel.AragUploadFolder);

            var data = new StringContent(keyValuePairs.ToString(), Encoding.UTF8, "application/json");
            var url = "https://app.staging.v2-demo.idefend.eu/admin/api/imports/send";

            var client = new HttpClient();
            var xeroResponseString = ProcessWebResponse(tracingService, client, url, data);
            var xeroResponse = xeroResponseString.Result;
            tracingService.Trace(xeroResponse);
        }

        public static async Task<string> ProcessWebResponse(ITracingService tracingService, HttpClient client, string url, StringContent request)
        {
            var reponseContentString = "";
            try
            {
                HttpResponseMessage response = await client.PostAsync(url, request);
                tracingService.Trace(response.ToString());
                reponseContentString = await response.Content.ReadAsStringAsync();
                tracingService.Trace(reponseContentString);
            }
            catch (HttpRequestException ex)
            {
                reponseContentString = ex.InnerException.Message;
                tracingService.Trace(reponseContentString);
            }
            return reponseContentString;
        }
    }
}
