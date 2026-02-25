using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Plugins
{
    public class AddListMembersList : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            try
            {
                IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

                if (context.InputParameters.Contains("List"))
                {
                    Entity listReference = (Entity)context.InputParameters["List"];
                    Guid listID = listReference.Id;

                    List<Guid> MemberIds = new List<Guid>();

                    EntityCollection Memberreference = (EntityCollection)context.InputParameters["Members"];

                    foreach (var item in Memberreference.Entities)
                    {
                        MemberIds.Add(item.Id);
                    }

                    var addMemberListReq = new AddListMembersListRequest
                    {
                        MemberIds = MemberIds.ToArray(),
                        ListId = listID
                    };
                    var response = organizationService.Execute(addMemberListReq);
                    context.OutputParameters["value"] = "Members Added Successfully to the List";
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
