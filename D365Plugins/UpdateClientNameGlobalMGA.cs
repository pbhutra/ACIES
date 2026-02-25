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
    public class UpdateClientNameGlobalMGA : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

            if (context.InputParameters.Contains("Target") && context.Depth == 1)
            {
                Entity entity = new Entity();
                if (context.MessageName != "Delete")
                {
                    Entity e = (Entity)context.InputParameters["Target"];
                    entity = organizationService.Retrieve(e.LogicalName, e.Id, new ColumnSet(true));
                }
                else
                {
                    EntityReference e = (EntityReference)context.InputParameters["Target"];
                    entity = organizationService.Retrieve(e.LogicalName, e.Id, new ColumnSet(true));
                }

                try
                {
                    if (entity.LogicalName == "lux_cpecompaniesdetails")
                    {
                        if (entity.Contains("lux_contractorsplantandequipmentquote"))
                        {
                            var appln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", entity.GetAttributeValue<EntityReference>("lux_contractorsplantandequipmentquote").Id, new ColumnSet(false));
                            var fetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_cpecompaniesdetails'>
                                            <attribute name='lux_name' />
                                            <attribute name='lux_postcode' />
                                            <attribute name='lux_companynumber' />
                                            <attribute name='lux_citycounty' />
                                            <attribute name='lux_addressline2' />
                                            <attribute name='lux_addressline1' />
                                            <attribute name='lux_countyprovince' />
                                            <attribute name='lux_creditscore' />
                                            <attribute name='lux_creditrating' />
                                            <attribute name='lux_ccj' />
                                            <attribute name='lux_matchstatus' />
                                            <attribute name='lux_risklevel' />
                                            <attribute name='lux_cpecompaniesdetailsid' />
                                            <order attribute='createdon' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{appln.Id}' />
                                            </filter>
                                          </entity>
                                        </fetch>";

                            if (context.MessageName == "Delete")
                            {
                                fetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_cpecompaniesdetails'>
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_postcode' />
                                                            <attribute name='lux_companynumber' />
                                                            <attribute name='lux_citycounty' />
                                                            <attribute name='lux_addressline2' />
                                                            <attribute name='lux_addressline1' />
                                                            <attribute name='lux_countyprovince' />
                                                            <attribute name='lux_creditscore' />
                                                            <attribute name='lux_creditrating' />
                                                            <attribute name='lux_ccj' />
                                                            <attribute name='lux_matchstatus' />
                                                            <attribute name='lux_risklevel' />
                                                            <attribute name='lux_cpecompaniesdetailsid' />
                                                            <order attribute='createdon' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{appln.Id}' />
                                                              <condition attribute='lux_cpecompaniesdetailsid' operator='ne' uiname='' uitype='lux_cpecompaniesdetails' value='{entity.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                            }

                            if (organizationService.RetrieveMultiple(new FetchExpression(fetch)).Entities.Count > 0)
                            {
                                appln["lux_client"] = String.Join(", ", organizationService.RetrieveMultiple(new FetchExpression(fetch)).Entities.ToList().Select(x => x.Attributes.Contains("lux_name") ? x.Attributes["lux_name"].ToString() : ""));
                                organizationService.Update(appln);
                            }
                            else
                            {
                                appln["lux_client"] = "";
                                organizationService.Update(appln);
                            }
                        }
                    }
                    else if (entity.LogicalName == "lux_portandterminalcompaniesdetail")
                    {
                        if (entity.Contains("lux_portandterminalsquote"))
                        {
                            var appln = organizationService.Retrieve("lux_portandterminalsquote", entity.GetAttributeValue<EntityReference>("lux_portandterminalsquote").Id, new ColumnSet(false));
                            var fetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_portandterminalcompaniesdetail'>
                                            <attribute name='lux_name' />
                                            <attribute name='lux_portandterminalcompaniesdetailid' />
                                            <order attribute='createdon' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <condition attribute='lux_portandterminalsquote' operator='eq' uiname='' uitype='lux_portandterminalsquote' value='{appln.Id}' />
                                            </filter>
                                          </entity>
                                        </fetch>";

                            if (context.MessageName == "Delete")
                            {
                                fetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_portandterminalcompaniesdetail'>
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_portandterminalcompaniesdetailid' />
                                                            <order attribute='createdon' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_portandterminalsquote' operator='eq' uiname='' uitype='lux_portandterminalsquote' value='{appln.Id}' />
                                                              <condition attribute='lux_portandterminalcompaniesdetailid' operator='ne' uiname='' uitype='lux_portandterminalcompaniesdetail' value='{entity.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                            }

                            if (organizationService.RetrieveMultiple(new FetchExpression(fetch)).Entities.Count > 0)
                            {
                                appln["lux_client"] = String.Join(", ", organizationService.RetrieveMultiple(new FetchExpression(fetch)).Entities.ToList().Select(x => x.Attributes.Contains("lux_name") ? x.Attributes["lux_name"].ToString() : ""));
                                organizationService.Update(appln);
                            }
                            else
                            {
                                appln["lux_client"] = "";
                                organizationService.Update(appln);
                            }
                        }
                    }
                    else if (entity.LogicalName == "lux_subscribepicompaniesdetail")
                    {
                        if (entity.Contains("lux_subscribeprofessionalindemnityquote"))
                        {
                            var appln = organizationService.Retrieve("lux_subscribepiquote", entity.GetAttributeValue<EntityReference>("lux_subscribeprofessionalindemnityquote").Id, new ColumnSet(false));
                            var fetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_subscribepicompaniesdetail'>
                                            <attribute name='lux_name' />
                                            <attribute name='lux_subscribepicompaniesdetailid' />
                                            <order attribute='createdon' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{appln.Id}' />
                                            </filter>
                                          </entity>
                                        </fetch>";

                            if (context.MessageName == "Delete")
                            {
                                fetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_subscribepicompaniesdetail'>
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_subscribepicompaniesdetailid' />
                                                            <order attribute='createdon' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{appln.Id}' />
                                                              <condition attribute='lux_subscribepicompaniesdetailid' operator='ne' uiname='' uitype='lux_subscribepicompaniesdetail' value='{entity.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                            }

                            if (organizationService.RetrieveMultiple(new FetchExpression(fetch)).Entities.Count > 0)
                            {
                                appln["lux_client"] = String.Join(", ", organizationService.RetrieveMultiple(new FetchExpression(fetch)).Entities.ToList().Select(x => x.Attributes.Contains("lux_name") ? x.Attributes["lux_name"].ToString() : ""));
                                organizationService.Update(appln);
                            }
                            else
                            {
                                appln["lux_client"] = "";
                                organizationService.Update(appln);
                            }
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