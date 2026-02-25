using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class SelectQuoteOptionPhoenix : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity && context.Depth == 1)
            {
                // Obtain the target entity from the input parameters.
                Entity entity = (Entity)context.InputParameters["Target"];
                try
                {
                    // Obtain the organization service reference.
                    IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

                    var phoenixQuoteOption = organizationService.Retrieve("lux_phoenixquoteoption", entity.Id, new ColumnSet(true));
                    var SelectedQuoteOption = phoenixQuoteOption.Attributes.Contains("lux_quoteoptionselected") ? phoenixQuoteOption.GetAttributeValue<bool>("lux_quoteoptionselected") : false;

                    var phoenixQuote = organizationService.Retrieve("lux_contractorsplantandequipmentquote", phoenixQuoteOption.GetAttributeValue<EntityReference>("lux_contractorsplantandequipmentquote").Id, new ColumnSet(false));
                    phoenixQuote["lux_quoteoptionselected"] = new EntityReference("lux_phoenixquoteoption", phoenixQuoteOption.Id);
                    organizationService.Update(phoenixQuote);

                    if (SelectedQuoteOption == true)
                    {
                        var OptionsFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_phoenixquoteoption'>
                                                <attribute name='lux_phoenixquoteoptionid' />
                                                <attribute name='lux_name' />
                                                <attribute name='createdon' />
                                                <order attribute='lux_name' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{phoenixQuote.Id}' />
                                                  <condition attribute='lux_phoenixquoteoptionid' operator='ne' uiname='' uitype='lux_phoenixquoteoption' value='{phoenixQuoteOption.Id}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                        var optionsList = organizationService.RetrieveMultiple(new FetchExpression(OptionsFetch));
                        if (optionsList.Entities.Count() > 0)
                        {
                            foreach (var item in optionsList.Entities)
                            {
                                item["lux_quoteoptionselected"] = false;
                                organizationService.Update(item);
                            }
                        }
                    }

                    var cpeQuote = organizationService.Retrieve("lux_contractorsplantandequipmentquote", phoenixQuote.Id, new ColumnSet("transactioncurrencyid"));
                    var selectedQuoteOption = organizationService.Retrieve("lux_phoenixquoteoption", phoenixQuoteOption.Id, new ColumnSet(true));

                    var tradefetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_contractorsplantandequipmentquotepremui'>
                                            <attribute name='lux_sectionreference' />
                                            <attribute name='lux_technicalpremium' />
                                            <attribute name='lux_ratingfigures' />
                                            <attribute name='lux_ratedeviation' />
                                            <attribute name='lux_policypremiumbeforetax' />
                                            <attribute name='lux_justificaiton' />                                           
                                            <attribute name='lux_brokercommissionamount' />
                                            <attribute name='lux_mgacommissionamount' />
                                            <attribute name='lux_mgucommissionamount' />
                                            <attribute name='lux_totaltax' />
                                            <attribute name='lux_policyfee' />
                                            <attribute name='lux_totaltaxamount' />
                                            <attribute name='lux_loaddiscount' />
                                            <attribute name='lux_comment' />
                                            <attribute name='lux_section' />
                                            <attribute name='lux_contractorsplantandequipmentquotepremuiid' />
                                            <order attribute='lux_sectionreference' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <condition attribute='lux_phoenixquoteoption' operator='eq' uiname='' uitype='lux_phoenixquoteoption' value='{selectedQuoteOption.Id}' />
                                            </filter>
                                          </entity>
                                        </fetch>";

                    var tradeList1 = organizationService.RetrieveMultiple(new FetchExpression(tradefetch1)).Entities;
                    var Count = tradeList1.Count();

                    var PolicyPremium = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_policypremiumbeforetax") ? x.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0);
                    var BrokerCommission = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_brokercommissionamount") ? x.GetAttributeValue<Money>("lux_brokercommissionamount").Value : 0);
                    var MGACommission = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_mgacommissionamount") ? x.GetAttributeValue<Money>("lux_mgacommissionamount").Value : 0);
                    var MGUCommission = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_mgucommissionamount") ? x.GetAttributeValue<Money>("lux_mgucommissionamount").Value : 0);
                    var TaxAmount = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_totaltaxamount") ? x.GetAttributeValue<Money>("lux_totaltaxamount").Value : 0);
                    var Tax = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_totaltax") ? x.GetAttributeValue<decimal>("lux_totaltax") : 0);
                    var Fee = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_policyfee") ? x.GetAttributeValue<Money>("lux_policyfee").Value : 0);

                    //throw new InvalidPluginExecutionException(tradefetch1.ToString());
                    if (PolicyPremium != 0)
                    {
                        cpeQuote["lux_policypremiumbeforetax"] = new Money(PolicyPremium);
                        cpeQuote["lux_policybrokercommissionamount"] = BrokerCommission;
                        cpeQuote["lux_policybrokercommissionpercentage"] = BrokerCommission * 100 / PolicyPremium;
                        cpeQuote["lux_policymgacommissionpercentage"] = MGACommission * 100 / PolicyPremium;
                        cpeQuote["lux_policyaciesmgucommissionpercentage"] = MGUCommission * 100 / PolicyPremium;
                        cpeQuote["lux_policytotaltaxamount"] = new Money(TaxAmount);
                        cpeQuote["lux_policytotaltax"] = TaxAmount * 100 / PolicyPremium;
                        cpeQuote["lux_policypolicyfee"] = new Money(Fee);
                    }
                    else
                    {
                        cpeQuote["lux_policypremiumbeforetax"] = new Money(0);
                        cpeQuote["lux_policybrokercommissionamount"] = BrokerCommission;
                        cpeQuote["lux_policybrokercommissionpercentage"] = null;
                        cpeQuote["lux_policymgacommissionpercentage"] = null;
                        cpeQuote["lux_policyaciesmgucommissionpercentage"] = null;
                        cpeQuote["lux_policytotaltaxamount"] = new Money(TaxAmount);
                        cpeQuote["lux_policytotaltax"] = null;
                        cpeQuote["lux_policypolicyfee"] = new Money(Fee);
                    }
                    organizationService.Update(cpeQuote);

                    var brokerFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_cpebrokeragent'>
                                                <attribute name='lux_percentageorflatfee' />
                                                <attribute name='lux_percentage' />
                                                <attribute name='lux_commissonamount' />
                                                <attribute name='lux_cpesection' />
                                                <attribute name='lux_companytype' />
                                                <attribute name='lux_company' />
                                                <attribute name='lux_commissionamountforsignedshare_base' />
                                                <attribute name='lux_commissonamountforsubgrid' />
                                                <attribute name='lux_cpebrokeragentid' />
                                                <order attribute='lux_companytype' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                  <condition attribute='lux_cpesection' operator='ne' value='972970001' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                    var brokerList = organizationService.RetrieveMultiple(new FetchExpression(brokerFetch)).Entities;
                    foreach (var item in brokerList)
                    {
                        var cpeSection = item.GetAttributeValue<OptionSetValue>("lux_cpesection").Value;
                        var SectionalPolicyPremium = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == cpeSection - 2).Sum(x => x.Attributes.Contains("lux_policypremiumbeforetax") ? x.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0);
                        item["lux_sectionalpolicypremiumbeforetax"] = new Money(SectionalPolicyPremium);
                        organizationService.Update(item);
                    }

                    var taxFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_cpequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_section' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_areweraisingthetaxorjustreporting' />
                                                    <attribute name='lux_cpequotetaxtypeid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                      <condition attribute='lux_section' operator='ne' value='972970001' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                    var taxList = organizationService.RetrieveMultiple(new FetchExpression(taxFetch)).Entities;
                    foreach (var item in taxList)
                    {
                        var cpeSection = item.GetAttributeValue<OptionSetValue>("lux_section").Value;
                        var SectionalPolicyPremium = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == cpeSection - 2).Sum(x => x.Attributes.Contains("lux_policypremiumbeforetax") ? x.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0);
                        item["lux_sectionalpolicypremiumbeforetax"] = new Money(SectionalPolicyPremium);
                        item["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                        organizationService.Update(item);
                    }

                    var feeFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_phoenixfeetable'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_section' />
                                                    <attribute name='lux_feepercentage' />
                                                    <attribute name='lux_feeamount' />
                                                    <attribute name='lux_feebasis' />
                                                    <attribute name='lux_phoenixfeetableid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                      <condition attribute='lux_section' operator='ne' value='972970001' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                    var feeList = organizationService.RetrieveMultiple(new FetchExpression(feeFetch)).Entities;
                    foreach (var item in feeList)
                    {
                        var cpeSection = item.GetAttributeValue<OptionSetValue>("lux_section").Value;
                        var SectionalPolicyPremium = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == cpeSection - 2).Sum(x => x.Attributes.Contains("lux_policypremiumbeforetax") ? x.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0);
                        item["lux_sectionalpolicypremiumbeforetax"] = new Money(SectionalPolicyPremium);
                        organizationService.Update(item);
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