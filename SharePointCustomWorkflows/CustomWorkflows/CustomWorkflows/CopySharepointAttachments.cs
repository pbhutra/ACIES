using Microsoft.SharePoint.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Linq;
using System.Security;

namespace CustomWorkflows
{
    public class CopySharepointAttachments : CodeActivity
    {
        [Input("Property Owners Application")]
        [ReferenceTarget("lux_propertyownersapplications")]
        public InArgument<EntityReference> PropertyOwnersApplication { get; set; }

        [Input("Old Property Owners Application")]
        [ReferenceTarget("lux_propertyownersapplications")]
        public InArgument<EntityReference> OldPropertyOwnersApplication { get; set; }

        [Input("Tradesman Application")]
        [ReferenceTarget("lux_tradesman")]
        public InArgument<EntityReference> TradesmanApplication { get; set; }

        [Input("Old Tradesman Application")]
        [ReferenceTarget("lux_tradesman")]
        public InArgument<EntityReference> OldTradesmanApplication { get; set; }

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

                EntityReference oldapplnref = OldPropertyOwnersApplication.Get<EntityReference>(executionContext);
                Entity oldappln = new Entity(oldapplnref.LogicalName, oldapplnref.Id);
                oldappln = service.Retrieve("lux_propertyownersapplications", oldapplnref.Id, new ColumnSet(true));

                var OldBrokerFolderName = oldappln.Attributes["lux_sharepointbrokerfoldername"].ToString();
                if (OldBrokerFolderName == "")
                    OldBrokerFolderName = oldappln.Attributes["lux_name"].ToString().Trim().Replace(".", "-").Replace("&", " - ").Replace("%", " - ").Replace(":", " - ").Replace("#", " - ").Replace("<", " - ").Replace(">", " - ").Replace("|", " - ").Replace("/", " - ").Replace("\"", " - ") + "_" + oldappln.Id.ToString().Replace("-", "") + "_Broker Documents";

                var OldPolicyFolderName = oldappln.Attributes["lux_sharepointpolicyfoldername"].ToString();
                if (OldPolicyFolderName == "")
                    OldPolicyFolderName = oldappln.Attributes["lux_name"].ToString().Trim().Replace(".", "-").Replace("&", " - ").Replace("%", " - ").Replace(":", " - ").Replace("#", " - ").Replace("<", " - ").Replace(">", " - ").Replace("|", " - ").Replace("/", " - ").Replace("\"", " - ") + "_" + oldappln.Id.ToString().Replace("-", "") + "_Policy Documents";

                var NewBrokerFolderName = appln.Attributes["lux_sharepointbrokerfoldername"].ToString();
                if (NewBrokerFolderName == "")
                    NewBrokerFolderName = appln.Attributes["lux_name"].ToString().Trim().Replace(".", "-").Replace("&", " - ").Replace("%", " - ").Replace(":", " - ").Replace("#", " - ").Replace("<", " - ").Replace(">", " - ").Replace("|", " - ").Replace("/", " - ").Replace("\"", " - ") + "_" + appln.Id.ToString().Replace("-", "") + "_Broker Documents";

                var NewPolicyFolderName = appln.Attributes["lux_sharepointpolicyfoldername"].ToString();
                if (NewPolicyFolderName == "")
                    NewPolicyFolderName = appln.Attributes["lux_name"].ToString().Trim().Replace(".", "-").Replace("&", " - ").Replace("%", " - ").Replace(":", " - ").Replace("#", " - ").Replace("<", " - ").Replace(">", " - ").Replace("|", " - ").Replace("/", " - ").Replace("\"", " - ") + "_" + appln.Id.ToString().Replace("-", "") + "_Policy Documents";

                using (ClientContext clientContext = new ClientContext("https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint"))
                {
                    SecureString passWord = new SecureString();
                    foreach (char c in "Nup496791".ToCharArray()) passWord.AppendChar(c);
                    clientContext.Credentials = new SharePointOnlineCredentials("lucidux@aciesmgu.com", passWord);

                    List list = clientContext.Web.Lists.GetByTitle("Application");
                    FileCollection files = list.RootFolder.Folders.GetByUrl(OldBrokerFolderName).Files;
                    clientContext.Load(files);
                    clientContext.ExecuteQuery();

                    if (files.Count() > 0)
                    {
                        foreach (File file in files)
                        {
                            if (clientContext.HasPendingRequest)
                                clientContext.ExecuteQuery();

                            FileCreationInformation fcInfo = new FileCreationInformation();
                            fcInfo.Overwrite = true;
                            fcInfo.Url = file.Name;

                            FileInformation fileinfo = Microsoft.SharePoint.Client.File.OpenBinaryDirect(clientContext, file.ServerRelativeUrl);

                            var memory = new System.IO.MemoryStream();
                            fileinfo.Stream.CopyTo(memory);
                            string strBase64 = Convert.ToBase64String(memory.ToArray());
                            fcInfo.ContentStream = new System.IO.MemoryStream(System.Convert.FromBase64String(strBase64));

                            try
                            {
                                var targetFolder = clientContext.Web.GetFolderByServerRelativeUrl(NewBrokerFolderName);
                                var uploadFile = targetFolder.Files.Add(fcInfo);
                                clientContext.Load(uploadFile);
                                clientContext.ExecuteQuery();
                            }
                            catch (Exception ex)
                            {
                                var targetFolder1 = clientContext.Web.GetFolderByServerRelativeUrl("https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint/lux_propertyownersapplications/");
                                var newTargetFolder = targetFolder1.Folders.Add(NewBrokerFolderName);
                                var uploadFile1 = newTargetFolder.Files.Add(fcInfo);
                                clientContext.Load(uploadFile1);
                                clientContext.ExecuteQuery();
                            }
                        }
                    }

                    List list1 = clientContext.Web.Lists.GetByTitle("Application");
                    FileCollection files1 = list1.RootFolder.Folders.GetByUrl(OldPolicyFolderName).Files;
                    clientContext.Load(files1);
                    clientContext.ExecuteQuery();

                    if (files1.Count() > 0)
                    {
                        foreach (File file in files1)
                        {
                            if (clientContext.HasPendingRequest)
                                clientContext.ExecuteQuery();

                            FileCreationInformation fcInfo = new FileCreationInformation();
                            fcInfo.Overwrite = true;
                            fcInfo.Url = file.Name;

                            FileInformation fileinfo = Microsoft.SharePoint.Client.File.OpenBinaryDirect(clientContext, file.ServerRelativeUrl);
                            var memory = new System.IO.MemoryStream();
                            fileinfo.Stream.CopyTo(memory);
                            string strBase64 = Convert.ToBase64String(memory.ToArray());
                            fcInfo.ContentStream = new System.IO.MemoryStream(System.Convert.FromBase64String(strBase64));

                            try
                            {
                                var targetFolder = clientContext.Web.GetFolderByServerRelativeUrl(NewPolicyFolderName);
                                var uploadFile = targetFolder.Files.Add(fcInfo);
                                clientContext.Load(uploadFile);
                                clientContext.ExecuteQuery();
                            }
                            catch (Exception ex)
                            {
                                var targetFolder1 = clientContext.Web.GetFolderByServerRelativeUrl("https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint/lux_propertyownersapplications/");
                                var newTargetFolder = targetFolder1.Folders.Add(NewPolicyFolderName);
                                var uploadFile1 = newTargetFolder.Files.Add(fcInfo);
                                clientContext.Load(uploadFile1);
                                clientContext.ExecuteQuery();
                            }
                        }
                    }
                }
            }
            else
            {
                EntityReference applnref = TradesmanApplication.Get<EntityReference>(executionContext);
                Entity appln = new Entity(applnref.LogicalName, applnref.Id);
                appln = service.Retrieve("lux_tradesman", applnref.Id, new ColumnSet(true));

                EntityReference noteref = Note.Get<EntityReference>(executionContext);
                Entity note = new Entity(noteref.LogicalName, noteref.Id);
                note = service.Retrieve("annotation", noteref.Id, new ColumnSet(true));

                var NewBrokerFolderName = appln.Attributes["lux_sharepointbrokerfoldername"].ToString();
                if (NewBrokerFolderName == "")
                    NewBrokerFolderName = appln.Attributes["lux_name"].ToString().Replace(".", "-").Replace("&", " - ").Replace("%", " - ").Replace(":", " - ").Replace("#", " - ").Replace("<", " - ").Replace(">", " - ").Replace("|", " - ").Replace("/", " - ").Replace("\"", " - ") + "_" + appln.Id.ToString().Replace("-", "") + "_Broker Documents";


                var notesFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                  <entity name='annotation'>
                                    <attribute name='subject' />
                                    <attribute name='notetext' />
                                    <attribute name='filename' />  
                                    <attribute name='documentbody' />
                                    <attribute name='annotationid' />
                                    <order attribute='subject' descending='false' />
                                    <link-entity name='lux_tradesman' from='lux_tradesmanid' to='objectid' link-type='inner' alias='ab'>
                                      <filter type='and'>
                                        <condition attribute='lux_tradesmanid' operator='eq' uitype='lux_tradesman' value='{appln.Id}' />
                                      </filter>
                                    </link-entity>
                                  </entity>
                                </fetch>";

                var file = service.RetrieveMultiple(new FetchExpression(notesFetch)).Entities;
                if (file.Count() > 0)
                {
                    using (ClientContext clientContext = new ClientContext("https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint"))
                    {
                        SecureString passWord = new SecureString();
                        foreach (char c in "Nup496791".ToCharArray()) passWord.AppendChar(c);
                        clientContext.Credentials = new SharePointOnlineCredentials("lucidux@aciesmgu.com", passWord);

                        foreach (var item in file)
                        {
                            FileCreationInformation fcInfo = new FileCreationInformation();
                            fcInfo.Overwrite = true;
                            fcInfo.Url = item.Attributes["filename"].ToString();
                            fcInfo.Content = System.Convert.FromBase64String(item.Attributes["documentbody"].ToString());

                            try
                            {
                                var targetFolder = clientContext.Web.GetFolderByServerRelativeUrl("https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint/lux_tradesman/" + NewBrokerFolderName);
                                var uploadFile = targetFolder.Files.Add(fcInfo);
                                clientContext.Load(uploadFile);
                                clientContext.ExecuteQuery();
                            }
                            catch (Exception ex)
                            {
                                var targetFolder1 = clientContext.Web.GetFolderByServerRelativeUrl("https://aciesltd.sharepoint.com/sites/ACIES-CRMSharePoint/lux_tradesman/");
                                var newTargetFolder = targetFolder1.Folders.Add(NewBrokerFolderName);
                                var uploadFile1 = newTargetFolder.Files.Add(fcInfo);
                                clientContext.Load(uploadFile1);
                                clientContext.ExecuteQuery();
                            }
                            service.Delete("annotation", item.Id);
                        }
                    }
                }
            }
        }
    }
}