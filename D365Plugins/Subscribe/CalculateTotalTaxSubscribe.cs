using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections;
using System.Linq;

namespace D365Plugins
{
    public class CalculateTotalTaxSubscribe : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            if (context.InputParameters.Contains("Target") && context.Depth == 1)
            {
                // Obtain the target entity from the input parameters.
                Entity entity = new Entity();
                try
                {
                    // Obtain the organization service reference.
                    IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

                    if (context.MessageName != "Delete")
                    {
                        entity = (Entity)context.InputParameters["Target"];
                    }
                    else
                    {
                        EntityReference e = (EntityReference)context.InputParameters["Target"];
                        entity = organizationService.Retrieve(e.LogicalName, e.Id, new ColumnSet(true));
                    }

                    var TaxRow = organizationService.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));
                    var subsQuote = organizationService.Retrieve("lux_subscribepiquote", TaxRow.GetAttributeValue<EntityReference>("lux_subscribeprofessionalindemnityquote").Id, new ColumnSet(true));
                    var quoteoption = subsQuote.Attributes.Contains("lux_wouldyouliketooffermultiplequoteoptions") ? subsQuote.FormattedValues["lux_wouldyouliketooffermultiplequoteoptions"] : "No";
                    var OptionCount = subsQuote.Contains("lux_quoteoptionscount") ? subsQuote.GetAttributeValue<int>("lux_quoteoptionscount") : 0;

                    if (context.MessageName == "Create")
                    {
                        if (subsQuote.Attributes.Contains("lux_quoteoption1"))
                        {
                            //var TaxRow1 = organizationService.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(false));
                            //TaxRow1["lux_subscribequoteoption"] = new EntityReference("lux_subscribequoteoption", subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption1").Id);
                            //organizationService.Update(TaxRow1);

                            if (OptionCount >= 1)
                            {
                                for (int i = 1; i <= OptionCount; i++)
                                {
                                    Entity subscribetax = new Entity(entity.LogicalName);
                                    subscribetax["lux_name"] = TaxRow["lux_name"].ToString();
                                    if (TaxRow.Attributes.Contains("lux_taxpercentage"))
                                    {
                                        subscribetax["lux_taxpercentage"] = TaxRow.GetAttributeValue<decimal>("lux_taxpercentage");
                                    }
                                    //throw new InvalidPluginExecutionException(subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption" + i).Id.ToString());
                                    subscribetax["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                    subscribetax["lux_subscribequoteoption"] = new EntityReference("lux_subscribequoteoption", subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption" + i).Id);
                                    subscribetax["lux_subscribeprofessionalindemnityquote"] = new EntityReference("lux_subscribepiquote", subsQuote.Id);
                                    organizationService.Create(subscribetax);
                                }
                            }
                        }
                    }
                    if (context.MessageName == "Update")
                    {
                        var Taxfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_technicaltaxamount' />
                                                    <attribute name='lux_subscribequotetaxtypeid' />
                                                    <attribute name='exchangerate' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribequotetaxtypeid' operator='ne' uiname='' uitype='lux_subscribequotetaxtype' value='{TaxRow.Id}' />
                                                      <condition attribute='lux_name' operator='eq' value='{TaxRow["lux_name"].ToString()}' />
                                                      <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subsQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                        var taxList = organizationService.RetrieveMultiple(new FetchExpression(Taxfetch));
                        if (taxList.Entities.Count() > 0)
                        {
                            foreach (var item in taxList.Entities)
                            {
                                if (TaxRow.Attributes.Contains("lux_taxpercentage"))
                                {
                                    item["lux_taxpercentage"] = TaxRow.GetAttributeValue<decimal>("lux_taxpercentage");
                                }
                                item["lux_name"] = TaxRow["lux_name"];
                                organizationService.Update(item);
                            }
                        }
                    }
                    if (context.MessageName == "Delete")
                    {
                        var Taxfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_technicaltaxamount' />
                                                    <attribute name='lux_subscribequotetaxtypeid' />
                                                    <attribute name='exchangerate' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribequotetaxtypeid' operator='ne' uiname='' uitype='lux_subscribequotetaxtype' value='{TaxRow.Id}' />
                                                      <condition attribute='lux_name' operator='eq' value='{TaxRow["lux_name"].ToString()}' />
                                                      <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subsQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";
                        var taxList = organizationService.RetrieveMultiple(new FetchExpression(Taxfetch));
                        if (taxList.Entities.Count() > 0)
                        {
                            foreach (var item in taxList.Entities)
                            {
                                organizationService.Delete("lux_subscribequotetaxtype", item.Id);
                            }
                        }
                    }

                    var TaxRowupdated = organizationService.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));

                    var TotalTaxfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_technicaltaxamount' />
                                                    <attribute name='lux_subscribequotetaxtypeid' />
                                                    <attribute name='exchangerate' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribequoteoption' operator='eq' uiname='' uitype='lux_subscribequoteoption' value='{subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption1").Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";
                    //throw new InvalidPluginExecutionException(TotalTaxfetch.ToString());
                    var totaltaxList = organizationService.RetrieveMultiple(new FetchExpression(TotalTaxfetch));
                    if (totaltaxList.Entities.Count() > 0)
                    {
                        var totalTax = totaltaxList.Entities.Sum(x => x.Attributes.Contains("lux_taxpercentage") ? x.GetAttributeValue<decimal>("lux_taxpercentage") : 0);

                        //Entity quoteOption = organizationService.Retrieve("lux_subscribequoteoption", subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption1").Id, new ColumnSet("lux_technicalpremiumbeforetax", "lux_policypremiumbeforetax"));

                        //var TechnicalPremium = quoteOption.Attributes.Contains("lux_technicalpremiumbeforetax") ? quoteOption.GetAttributeValue<Money>("lux_technicalpremiumbeforetax").Value : 0;
                        //var PolicyPremium = quoteOption.Attributes.Contains("lux_policypremiumbeforetax") ? quoteOption.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : TechnicalPremium;

                        //quoteOption["lux_technicaltotaltaxamount"] = new Money(TechnicalPremium * totalTax / 100);
                        //quoteOption["lux_policytotaltaxamount"] = new Money(PolicyPremium * totalTax / 100);

                        //quoteOption["lux_policytotaltax"] = totalTax;
                        //organizationService.Update(quoteOption);

                        if (OptionCount >= 1)
                        {
                            for (int i = 1; i <= OptionCount; i++)
                            {
                                Entity quoteOption1 = organizationService.Retrieve("lux_subscribequoteoption", subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption" + i).Id, new ColumnSet("lux_technicalpremiumbeforetax", "lux_policypremiumbeforetax"));

                                var TechnicalPremium1 = quoteOption1.Attributes.Contains("lux_technicalpremiumbeforetax") ? quoteOption1.GetAttributeValue<Money>("lux_technicalpremiumbeforetax").Value : 0;
                                var PolicyPremium1 = quoteOption1.Attributes.Contains("lux_policypremiumbeforetax") ? quoteOption1.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : TechnicalPremium1;

                                quoteOption1["lux_technicaltotaltaxamount"] = new Money(TechnicalPremium1 * totalTax / 100);
                                quoteOption1["lux_policytotaltaxamount"] = new Money(PolicyPremium1 * totalTax / 100);

                                quoteOption1["lux_policytotaltax"] = totalTax;
                                organizationService.Update(quoteOption1);
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