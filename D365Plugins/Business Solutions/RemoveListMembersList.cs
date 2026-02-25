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
    public class RemoveListMembersList : IPlugin
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

                    EntityCollection Memberreference = (EntityCollection)context.InputParameters["Members"];

                    foreach (var item in Memberreference.Entities)
                    {
                        var removeMemberListReq = new RemoveMemberListRequest
                        {
                            EntityId = item.Id,
                            ListId = listID
                        };
                        organizationService.Execute(removeMemberListReq);
                    }
                    context.OutputParameters["value"] = "Members Removed Successfully from the List";
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
