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
    public class UpdateClientNameonAssociateDissociate : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is EntityReference && context.Depth == 1)
            {
                EntityReference e = (EntityReference)context.InputParameters["Target"];
                if (e.LogicalName != "lux_constructionquotes" && e.LogicalName != "lux_phoenixliabilityquote")
                {
                    return;
                }

                Entity entity = organizationService.Retrieve(e.LogicalName, e.Id, new ColumnSet(true));

                try
                {
                    if (e.LogicalName == "lux_constructionquotes")
                    {
                        var appln = organizationService.Retrieve("lux_constructionquotes", entity.Id, new ColumnSet(false));

                        var fetch = $@"<fetch name='table1' relationshipname='lux_contact_lux_constructionquotes' mapping='logical'>
                             <entity name='contact'>                             
                                  <attribute name='address1_postalcode' />
                                  <attribute name='address1_composite' />
                                  <attribute name='createdon' />
                                  <attribute name='fullname' />
                                  <attribute name='lux_matchstatus' />
                                  <order attribute='createdon' descending='false' priority='1000' sorttype='datetime' />
                                  <link-entity relationshipname='MTOMFilter' name='lux_contact_lux_constructionquotes' from='contactid' visible='false' intersect='true'>
                                       <link-entity name='lux_constructionquotes' to='lux_constructionquotesid'>
                                            <filter type='and'>
                                                 <condition attribute='lux_constructionquotesid' operator='in'>
                                                      <value>{appln.Id}</value>
                                                 </condition>
                                            </filter>
                                       </link-entity>
                                  </link-entity>
                             </entity>
                        </fetch>";

                        if (organizationService.RetrieveMultiple(new FetchExpression(fetch)).Entities.Count > 0)
                        {
                            appln["lux_client"] = String.Join(", ", organizationService.RetrieveMultiple(new FetchExpression(fetch)).Entities.ToList().Select(x => x.Attributes.Contains("fullname") ? x.Attributes["fullname"].ToString() : ""));
                            organizationService.Update(appln);
                        }
                    }
                    else if (e.LogicalName == "lux_phoenixliabilityquote")
                    {
                        var appln = organizationService.Retrieve("lux_phoenixliabilityquote", entity.Id, new ColumnSet(false));

                        var fetch = $@"<fetch name='table1' relationshipname='lux_contact_lux_phoenixliabilityquote' mapping='logical'>
                             <entity name='contact'>                             
                                  <attribute name='address1_postalcode' />
                                  <attribute name='address1_composite' />
                                  <attribute name='createdon' />
                                  <attribute name='fullname' />
                                  <attribute name='lux_matchstatus' />
                                  <order attribute='createdon' descending='false' priority='1000' sorttype='datetime' />
                                  <link-entity relationshipname='MTOMFilter' name='lux_contact_lux_phoenixliabilityquote' from='contactid' visible='false' intersect='true'>
                                       <link-entity name='lux_phoenixliabilityquote' to='lux_phoenixliabilityquoteid'>
                                            <filter type='and'>
                                                 <condition attribute='lux_phoenixliabilityquoteid' operator='in'>
                                                      <value>{appln.Id}</value>
                                                 </condition>
                                            </filter>
                                       </link-entity>
                                  </link-entity>
                             </entity>
                        </fetch>";

                        if (organizationService.RetrieveMultiple(new FetchExpression(fetch)).Entities.Count > 0)
                        {
                            appln["lux_client"] = String.Join(", ", organizationService.RetrieveMultiple(new FetchExpression(fetch)).Entities.ToList().Select(x => x.Attributes.Contains("fullname") ? x.Attributes["fullname"].ToString() : ""));
                            organizationService.Update(appln);
                        }
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }
    }
}