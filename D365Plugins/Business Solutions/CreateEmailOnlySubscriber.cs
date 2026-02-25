using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Plugins
{
    public class CreateEmailOnlySubscriber : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            try
            {
                IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

                var emailaddress = context.InputParameters.Contains("EmailAddress") ? context.InputParameters["EmailAddress"].ToString() : "";

                var ContactFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='contact'>
                                            <attribute name='telephone1' />
                                            <attribute name='emailaddress1' />
                                            <attribute name='contactid' />
                                            <order attribute='fullname' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='emailaddress1' operator='eq' value='{emailaddress}' />
                                            </filter>
                                          </entity>
                                        </fetch>";

                var ContactList = organizationService.RetrieveMultiple(new FetchExpression(ContactFetch));
                if (ContactList.Entities.Count() > 0)
                {
                    var ContactID = ContactList.Entities.FirstOrDefault().Id;

                    List<Guid> MemberIds = new List<Guid>();
                    MemberIds.Add(ContactID);

                    var addMemberListReq = new AddListMembersListRequest
                    {
                        MemberIds = MemberIds.ToArray(),
                        ListId = new Guid("05e05e78-a6c7-ed11-b597-0022481b5488")
                    };
                    var response = organizationService.Execute(addMemberListReq);
                    context.OutputParameters["value"] = "Subscriber Created and Added to the Investor List";
                }
                else
                {
                    Entity contact = new Entity("contact");
                    contact["emailaddress1"] = emailaddress;
                    var ContactID = organizationService.Create(contact);

                    List<Guid> MemberIds = new List<Guid>();
                    MemberIds.Add(ContactID);

                    var addMemberListReq = new AddListMembersListRequest
                    {
                        MemberIds = MemberIds.ToArray(),
                        ListId = new Guid("05e05e78-a6c7-ed11-b597-0022481b5488")
                    };
                    var response = organizationService.Execute(addMemberListReq);
                    context.OutputParameters["value"] = "Subscriber Created and Added to the Investor List";
                }
            }
            catch (Exception ex)
            {
                throw ex.InnerException;
            }
        }
    }
}
