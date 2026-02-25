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
    public class MarketingList : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            try
            {
                IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

                var Email = context.InputParameters.Contains("email") ? context.InputParameters["email"].ToString() : "";
                if (Email != "")
                {
                    EntityCollection collection = new EntityCollection();
                    var MarketingListFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='true'>
                                                  <entity name='list'>
                                                    <attribute name='listname' />
                                                    <attribute name='purpose' />
                                                    <attribute name='ownerid' />
                                                    <attribute name='modifiedon' />
                                                    <attribute name='lux_preferencecentrelist' />
                                                    <attribute name='lux_newslettertype' />
                                                    <attribute name='membercount' />
                                                    <attribute name='lux_entity' />
                                                    <attribute name='lux_brand' />
                                                    <attribute name='listid' />
                                                    <order attribute='listname' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='msdyncrm_issubscription' operator='eq' value='1' />
                                                    </filter>
                                                    <link-entity name='listmember' from='listid' to='listid' visible='false' intersect='true'>
                                                      <link-entity name='contact' from='contactid' to='entityid' alias='ad'>
                                                        <filter type='and'>
                                                          <condition attribute='emailaddress1' operator='eq' value='{Email}' />
                                                        </filter>
                                                      </link-entity>
                                                    </link-entity>
                                                  </entity>
                                                </fetch>";

                    var MarketingList = organizationService.RetrieveMultiple(new FetchExpression(MarketingListFetch));
                    if (MarketingList.Entities.Count() > 0)
                    {
                        foreach (var item in MarketingList.Entities)
                        {
                            Entity ent = new Entity();
                            ent.Attributes["listid"] = item.Id.ToString();
                            ent.Attributes["listname"] = item.Attributes["listname"].ToString();

                            collection.Entities.Add(ent);
                        }
                        context.OutputParameters["value"] = collection;
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
