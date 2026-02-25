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
    public class CheckForSizeDiscount : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity && context.Depth == 1)
            {
                Entity entity = context.InputParameters["Target"] as Entity;

                var Section = 0;
                var ProductID = "";

                if (entity.Attributes.ContainsKey("lux_section") && entity.Attributes["lux_section"] != null)
                    Section = entity.GetAttributeValue<OptionSetValue>("lux_section").Value;

                if (entity.Attributes.ContainsKey("lux_product") && entity.Attributes["lux_product"] != null)
                    ProductID = entity.GetAttributeValue<EntityReference>("lux_product").Id.ToString();

                try
                {
                    var fetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                      <entity name='lux_sizediscount'>
                                        <attribute name='lux_effectiveto' />
                                        <attribute name='lux_effectivefrom' />
                                        <attribute name='lux_below500k' />
                                        <attribute name='lux_above5m' />
                                        <attribute name='lux_501k1m' />
                                        <attribute name='lux_20000015m' />
                                        <attribute name='lux_10000012m' />
                                        <attribute name='lux_sizediscountid' />
                                        <order attribute='createdon' descending='false' />
                                        <filter type='and'>
                                          <condition attribute='statecode' operator='eq' value='0' />
                                          <condition attribute='lux_section' operator='eq' value='{Section}' />
                                          <condition attribute='lux_product' operator='eq' uiname='' uitype='product' value='{ProductID}' />
                                        </filter>
                                      </entity>
                                    </fetch>";

                    var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_sizediscount'>
                                            <attribute name='lux_effectiveto' />
                                            <attribute name='lux_effectivefrom' />
                                            <attribute name='lux_below500k' />
                                            <attribute name='lux_above5m' />
                                            <attribute name='lux_501k1m' />
                                            <attribute name='lux_20000015m' />
                                            <attribute name='lux_10000012m' />
                                            <attribute name='lux_sizediscountid' />
                                            <order attribute='createdon' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <condition attribute='lux_product' operator='eq' uiname='' uitype='product' value='{ProductID}' />
                                              <condition attribute='lux_section' operator='eq' value='{Section}' />
                                              <condition attribute='lux_effectiveto' operator='null' />
                                            </filter>
                                          </entity>
                                        </fetch>";

                    //throw new InvalidPluginExecutionException(organizationService.RetrieveMultiple(new FetchExpression(fetch)).Entities.Count + "" + organizationService.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count);

                    if (organizationService.RetrieveMultiple(new FetchExpression(fetch)).Entities.Count != 0 && organizationService.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                    {
                        throw new InvalidPluginExecutionException("Please put end date on Old Size Discount line before setting up new line");
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