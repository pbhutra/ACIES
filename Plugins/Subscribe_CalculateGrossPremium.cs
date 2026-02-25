using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace Acies_Customization.Plugins
{
    public class Subscribe_CalculateGrossPremium : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (context.InputParameters.Contains("Target") && context.Depth == 1)
            {
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
                    
                    var subscribeQuoteId = "";
                    var SelectedQuoteOptionId = "";
                    if (entity.LogicalName == "lux_subscribepisectionpremium")
                    {
                        Entity PremiumRow = organizationService.Retrieve("lux_subscribepisectionpremium", entity.Id, new ColumnSet(true));

                        if (PremiumRow.Attributes.Contains("lux_subscribequoteoption"))
                        {
                            var QuoteOption = organizationService.Retrieve("lux_subscribequoteoption", PremiumRow.GetAttributeValue<EntityReference>("lux_subscribequoteoption").Id, new ColumnSet(true));
                            var subscribeQuote = organizationService.Retrieve("lux_subscribepiquote", QuoteOption.GetAttributeValue<EntityReference>("lux_subscribeprofessionalindemnityquote").Id, new ColumnSet("lux_quoteoptions", "lux_applicationtype"));
                            subscribeQuoteId = subscribeQuote.Id.ToString();
                            SelectedQuoteOptionId = subscribeQuote.GetAttributeValue<EntityReference>("lux_quoteoptions").Id.ToString();
                            var ApplicationType = subscribeQuote.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value;

                            var tradefetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_subscribepisectionpremium'>
                                            <attribute name='lux_sectionreference' />
                                            <attribute name='lux_technicalpremium' />
                                            <attribute name='lux_ratingfigures' />
                                            <attribute name='lux_ratedeviation' />
                                            <attribute name='lux_policypremiumbeforetax' />
                                            <attribute name='lux_justificaiton' />
                                            <attribute name='lux_loaddiscount' />
                                            <attribute name='lux_comment' />
                                            <attribute name='lux_section' />
                                            <attribute name='lux_subscribepisectionpremiumid' />
                                            <order attribute='lux_sectionreference' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <condition attribute='lux_subscribequoteoption' operator='eq' uiname='' uitype='lux_subscribequoteoption' value='{QuoteOption.Id}' />
                                            </filter>
                                          </entity>
                                        </fetch>";
                            var tradeList1 = organizationService.RetrieveMultiple(new FetchExpression(tradefetch1));

                            var brokerfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                      <entity name='lux_subscribebrokersagent'>
                                                        <attribute name='lux_pisection' />
                                                        <attribute name='lux_percentageorflatfee' />
                                                        <attribute name='lux_broker' />
                                                        <attribute name='lux_percentage' />
                                                        <attribute name='lux_companytype' />
                                                        <attribute name='lux_commissonamount' />
                                                        <attribute name='lux_subscribebrokersagentid' />
                                                        <order attribute='lux_broker' descending='false' />
                                                        <filter type='and'>
                                                          <condition attribute='statecode' operator='eq' value='0' />
                                                          <condition attribute='lux_subscribepiquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subscribeQuote.Id}' />
                                                        </filter>
                                                      </entity>
                                                    </fetch>";
                            var brokerList = organizationService.RetrieveMultiple(new FetchExpression(brokerfetch));

                            var Taxfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_pisection' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_areweraisingthetaxorjustreporting' />
                                                    <attribute name='lux_subscribequotetaxtypeid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subscribeQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                            var taxList = organizationService.RetrieveMultiple(new FetchExpression(Taxfetch));

                            var Feefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribefeetable'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_pisection' />
                                                    <attribute name='lux_feepercentage' />
                                                    <attribute name='lux_feeamount' />
                                                    <attribute name='lux_feebasis' />
                                                    <attribute name='lux_subscribefeetableid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subscribeQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                            var FeeList = organizationService.RetrieveMultiple(new FetchExpression(Feefetch));

                            foreach (var item in tradeList1.Entities)
                            {
                                var tradeCover = item.Contains("lux_section") ? item.GetAttributeValue<OptionSetValue>("lux_section").Value : 0;
                                var PolicyPremium = item.Attributes.Contains("lux_policypremiumbeforetax") ? item.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0;

                                if (PolicyPremium != 0)
                                {
                                    var MGACommPercentage = 0M;
                                    var MGACommflatfee = 0M;
                                    var MGUCommPercentage = 0M;
                                    var MGUCommflatfee = 0M;
                                    var BrokerCommPercentage = 0M;
                                    var BrokerCommflatfee = 0M;
                                    var PolicyTaxRate = 0M;
                                    var PolicyPercentageFee = 0M;
                                    var PolicyFlatFee = 0M;

                                    if (brokerList.Entities.Count() > 0)
                                    {
                                        MGACommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970001 && (x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == tradeCover || x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == 972970005)).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        MGACommflatfee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970001 && (x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == tradeCover || x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == 972970005)).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);

                                        MGUCommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970002 && (x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == tradeCover || x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == 972970005)).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        MGUCommflatfee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970002 && (x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == tradeCover || x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == 972970005)).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);

                                        BrokerCommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970002 && (x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == tradeCover || x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == 972970005)).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        BrokerCommflatfee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970002 && (x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == tradeCover || x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == 972970005)).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);

                                        var PolicyMGACommAmt = PolicyPremium * (MGACommPercentage) / 100 + MGACommflatfee;
                                        var PolicyMGUCommAmt = PolicyPremium * (MGUCommPercentage) / 100 + MGUCommflatfee;
                                        var PolicyBrokerCommAmt = PolicyPremium * (BrokerCommPercentage) / 100 + BrokerCommflatfee;

                                        item["lux_mgacommission"] = PolicyMGACommAmt * 100 / PolicyPremium;
                                        item["lux_mgucommission"] = PolicyMGUCommAmt * 100 / PolicyPremium;
                                        item["lux_brokercommission"] = PolicyBrokerCommAmt * 100 / PolicyPremium;
                                        item["lux_totalcommission"] = (PolicyMGACommAmt * 100 / PolicyPremium) + (PolicyBrokerCommAmt * 100 / PolicyPremium);
                                    }

                                    if (taxList.Entities.Count() > 0)
                                    {
                                        PolicyTaxRate = taxList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_areweraisingthetaxorjustreporting").Value != 972970003 && (x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == tradeCover || x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == 972970005)).Sum(x => x.Attributes.Contains("lux_taxpercentage") ? x.GetAttributeValue<decimal>("lux_taxpercentage") : 0);
                                        item["lux_totaltax"] = PolicyTaxRate;
                                    }

                                    if (FeeList.Entities.Count() > 0)
                                    {
                                        PolicyPercentageFee = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == tradeCover && x.GetAttributeValue<OptionSetValue>("lux_pisection").Value != 972970005).Sum(x => x.Attributes.Contains("lux_feepercentage") ? x.GetAttributeValue<decimal>("lux_feepercentage") : 0);
                                        PolicyFlatFee = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == tradeCover && x.GetAttributeValue<OptionSetValue>("lux_pisection").Value != 972970005).Sum(x => x.Attributes.Contains("lux_feeamount") ? x.GetAttributeValue<Money>("lux_feeamount").Value : 0);
                                        var PolicyFeeAmt = (PolicyPremium * PolicyPercentageFee / 100) + PolicyFlatFee;

                                        if (ApplicationType == 972970001)
                                        {
                                            item["lux_policyfee"] = new Money(PolicyFeeAmt);
                                        }
                                        else if (ApplicationType == 972970002)
                                        {
                                            item["lux_mtapolicyfee"] = new Money(PolicyFeeAmt);
                                        }
                                    }
                                    organizationService.Update(item);
                                }
                            }

                            var tradefetch11 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_subscribepisectionpremium'>
                                            <attribute name='lux_sectionreference' />
                                            <attribute name='lux_technicalpremium' />
                                            <attribute name='lux_ratingfigures' />
                                            <attribute name='lux_ratedeviation' />
                                            <attribute name='lux_policypremium' />
                                            <attribute name='lux_policypremiumbeforetax' />
                                            <attribute name='lux_justificaiton' />
                                            <attribute name='lux_loaddiscount' />
                                            <attribute name='lux_policyfee' />
                                            <attribute name='lux_brokercommissionamount' />
                                            <attribute name='lux_mgacommissionamount' />
                                            <attribute name='lux_mgucommissionamount' />   
                                            <attribute name='lux_totaltaxamount' />
                                            <attribute name='lux_comment' />
                                            <attribute name='lux_section' />
                                            <attribute name='lux_subscribepisectionpremiumid' />
                                            <order attribute='lux_sectionreference' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <condition attribute='lux_subscribequoteoption' operator='eq' uiname='' uitype='lux_subscribequoteoption' value='{QuoteOption.Id}' />
                                            </filter>
                                          </entity>
                                        </fetch>";

                            var tradeList11 = organizationService.RetrieveMultiple(new FetchExpression(tradefetch11)).Entities;

                            Entity totalRow = organizationService.Retrieve("lux_subscribepisectionpremium", tradeList11.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970006).Id, new ColumnSet(false));
                            totalRow["lux_ratingfigures"] = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_ratingfigures") ? x.GetAttributeValue<Money>("lux_ratingfigures").Value : 0);

                            var technicalPrem = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_technicalpremium") ? x.GetAttributeValue<Money>("lux_technicalpremium").Value : 0);
                            var policyPrem = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_policypremium") ? x.GetAttributeValue<Money>("lux_policypremium").Value : 0);
                            var policyPrembeforeTax = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_policypremiumbeforetax") ? x.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0);

                            var BrokerCommission = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_brokercommissionamount") ? x.GetAttributeValue<Money>("lux_brokercommissionamount").Value : 0);
                            var MGACommission = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_mgacommissionamount") ? x.GetAttributeValue<Money>("lux_mgacommissionamount").Value : 0);
                            var MGUCommission = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_mgucommissionamount") ? x.GetAttributeValue<Money>("lux_mgucommissionamount").Value : 0);

                            var TaxAmount = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_totaltaxamount") ? x.GetAttributeValue<Money>("lux_totaltaxamount").Value : 0);
                            var Fee = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_policyfee") ? x.GetAttributeValue<Money>("lux_policyfee").Value : 0);

                            var PolicyPercentageFeeOverall = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == 972970005).Sum(x => x.Attributes.Contains("lux_feepercentage") ? x.GetAttributeValue<decimal>("lux_feepercentage") : 0);
                            var PolicyFlatFeeOverall = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == 972970005).Sum(x => x.Attributes.Contains("lux_feeamount") ? x.GetAttributeValue<Money>("lux_feeamount").Value : 0);
                            var PolicyFeeAmtOverall = (policyPrembeforeTax * PolicyPercentageFeeOverall / 100) + PolicyFlatFeeOverall;

                            if (policyPrembeforeTax != 0)
                            {
                                totalRow["lux_technicalpremium"] = new Money(technicalPrem);
                                totalRow["lux_loaddiscount"] = (policyPrem * 100 / technicalPrem) - 100;
                                totalRow["lux_justificaiton"] = "NA";
                                totalRow["lux_brokercommission"] = BrokerCommission * 100 / policyPrembeforeTax;
                                totalRow["lux_mgacommission"] = MGACommission * 100 / policyPrembeforeTax;
                                totalRow["lux_mgucommission"] = MGUCommission * 100 / policyPrembeforeTax;
                                totalRow["lux_totaltax"] = TaxAmount * 100 / policyPrembeforeTax;
                                if (ApplicationType == 972970001)
                                {
                                    totalRow["lux_policyfee"] = new Money(Fee + PolicyFeeAmtOverall);
                                }
                                else if (ApplicationType == 972970002)
                                {
                                    totalRow["lux_mtapolicyfee"] = new Money(Fee + PolicyFeeAmtOverall);
                                }
                                organizationService.Update(totalRow);
                            }
                        }
                    }
                    else
                    {
                        Entity subscribeEntity = new Entity();
                        if (entity.LogicalName == "lux_subscribebrokersagent")
                        {
                            subscribeEntity = organizationService.Retrieve("lux_subscribebrokersagent", entity.Id, new ColumnSet(true));
                        }
                        else if (entity.LogicalName == "lux_subscribequotetaxtype")
                        {
                            subscribeEntity = organizationService.Retrieve("lux_subscribequotetaxtype", entity.Id, new ColumnSet(true));
                        }
                        else if (entity.LogicalName == "lux_subscribefeetable")
                        {
                            subscribeEntity = organizationService.Retrieve("lux_subscribefeetable", entity.Id, new ColumnSet(true));
                        }

                        if (subscribeEntity.Attributes.Contains("lux_subscribeprofessionalindemnityquote") || subscribeEntity.Attributes.Contains("lux_subscribepiquote"))
                        {
                            var subscribeQuote = new Entity();
                            if (subscribeEntity.Attributes.Contains("lux_subscribeprofessionalindemnityquote"))
                            {
                                subscribeQuote = organizationService.Retrieve("lux_subscribepiquote", subscribeEntity.GetAttributeValue<EntityReference>("lux_subscribeprofessionalindemnityquote").Id, new ColumnSet(true));
                            }
                            else
                            {
                                subscribeQuote = organizationService.Retrieve("lux_subscribepiquote", subscribeEntity.GetAttributeValue<EntityReference>("lux_subscribepiquote").Id, new ColumnSet(true));
                            }
                            subscribeQuoteId = subscribeQuote.Id.ToString();
                            SelectedQuoteOptionId = subscribeQuote.Contains("lux_quoteoptions") ? subscribeQuote.GetAttributeValue<EntityReference>("lux_quoteoptions").Id.ToString() : "";
                            var ApplicationType = subscribeQuote.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value;

                            var subscribePremium = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_subscribepisectionpremium'>
                                            <attribute name='lux_sectionreference' />
                                            <attribute name='lux_technicalpremium' />
                                            <attribute name='lux_ratingfigures' />
                                            <attribute name='lux_ratedeviation' />
                                            <attribute name='lux_policypremiumbeforetax' />
                                            <attribute name='lux_justificaiton' />
                                            <attribute name='lux_loaddiscount' />
                                            <attribute name='lux_comment' />
                                            <attribute name='lux_section' />
                                            <attribute name='lux_subscribepisectionpremiumid' />
                                            <order attribute='lux_sectionreference' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                            </filter>
                                            <link-entity name='lux_subscribequoteoption' from='lux_subscribequoteoptionid' to='lux_subscribequoteoption' link-type='inner' alias='ab'>
                                                <filter type='and'>
                                                  <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subscribeQuote.Id}' />
                                                </filter>
                                            </link-entity>
                                          </entity>
                                        </fetch>";

                            var subscribePremiumLst = organizationService.RetrieveMultiple(new FetchExpression(subscribePremium));

                            var brokerfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                      <entity name='lux_subscribebrokersagent'>
                                                        <attribute name='lux_pisection' />
                                                        <attribute name='lux_percentageorflatfee' />
                                                        <attribute name='lux_broker' />
                                                        <attribute name='lux_percentage' />
                                                        <attribute name='lux_companytype' />
                                                        <attribute name='lux_commissonamount' />
                                                        <attribute name='lux_subscribebrokersagentid' />
                                                        <order attribute='lux_broker' descending='false' />
                                                        <filter type='and'>
                                                          <condition attribute='statecode' operator='eq' value='0' />
                                                          <condition attribute='lux_subscribepiquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subscribeQuote.Id}' />
                                                        </filter>
                                                      </entity>
                                                    </fetch>";
                            if (context.MessageName == "Delete" && entity.LogicalName == "lux_subscribebrokersagent")
                            {
                                brokerfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                      <entity name='lux_subscribebrokersagent'>
                                                        <attribute name='lux_pisection' />
                                                        <attribute name='lux_percentageorflatfee' />
                                                        <attribute name='lux_broker' />
                                                        <attribute name='lux_percentage' />
                                                        <attribute name='lux_companytype' />
                                                        <attribute name='lux_commissonamount' />
                                                        <attribute name='lux_subscribebrokersagentid' />
                                                        <order attribute='lux_broker' descending='false' />
                                                        <filter type='and'>
                                                          <condition attribute='statecode' operator='eq' value='0' />
                                                          <condition attribute='lux_subscribepiquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subscribeQuote.Id}' />
                                                          <condition attribute='lux_subscribebrokersagentid' operator='ne' uiname='' uitype='lux_subscribebrokersagent' value='{entity.Id}' />
                                                        </filter>
                                                      </entity>
                                                    </fetch>";
                            }

                            var brokerList = organizationService.RetrieveMultiple(new FetchExpression(brokerfetch));

                            var Taxfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_pisection' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_areweraisingthetaxorjustreporting' />
                                                    <attribute name='lux_subscribequotetaxtypeid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subscribeQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";
                            if (context.MessageName == "Delete" && entity.LogicalName == "lux_subscribequotetaxtype")
                            {
                                Taxfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_pisection' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_areweraisingthetaxorjustreporting' />
                                                    <attribute name='lux_subscribequotetaxtypeid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subscribeQuote.Id}' />
                                                      <condition attribute='lux_subscribequotetaxtypeid' operator='ne' uiname='' uitype='lux_subscribequotetaxtype' value='{entity.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";
                            }
                            var taxList = organizationService.RetrieveMultiple(new FetchExpression(Taxfetch));

                            var Feefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribefeetable'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_pisection' />
                                                    <attribute name='lux_feepercentage' />
                                                    <attribute name='lux_feeamount' />
                                                    <attribute name='lux_feebasis' />
                                                    <attribute name='lux_subscribefeetableid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subscribeQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";
                            if (context.MessageName == "Delete" && entity.LogicalName == "lux_subscribefeetable")
                            {
                                Feefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribefeetable'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_pisection' />
                                                    <attribute name='lux_feepercentage' />
                                                    <attribute name='lux_feeamount' />
                                                    <attribute name='lux_feebasis' />
                                                    <attribute name='lux_subscribefeetableid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subscribeQuote.Id}' />
                                                      <condition attribute='lux_subscribefeetableid' operator='ne' uiname='' uitype='lux_subscribefeetable' value='{entity.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";
                            }

                            var FeeList = organizationService.RetrieveMultiple(new FetchExpression(Feefetch));

                            if (subscribePremiumLst.Entities.Count > 0)
                            {
                                foreach (var item in subscribePremiumLst.Entities)
                                {
                                    var tradeCover = item.Contains("lux_section") ? item.GetAttributeValue<OptionSetValue>("lux_section").Value : 0;
                                    var PolicyPremium = item.Attributes.Contains("lux_policypremiumbeforetax") ? item.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0;
                                    if (PolicyPremium != 0)
                                    {
                                        var MGACommPercentage = 0M;
                                        var MGACommflatfee = 0M;
                                        var MGUCommPercentage = 0M;
                                        var MGUCommflatfee = 0M;
                                        var BrokerCommPercentage = 0M;
                                        var BrokerCommflatfee = 0M;

                                        if (brokerList.Entities.Count() > 0)
                                        {
                                            MGACommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970001 && (x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == tradeCover || x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == 972970005)).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            MGACommflatfee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970001 && (x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == tradeCover || x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == 972970005)).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);

                                            MGUCommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970002 && (x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == tradeCover || x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == 972970005)).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            MGUCommflatfee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970002 && (x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == tradeCover || x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == 972970005)).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);

                                            BrokerCommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970002 && (x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == tradeCover || x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == 972970005)).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            BrokerCommflatfee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970002 && (x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == tradeCover || x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == 972970005)).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);

                                            var PolicyMGACommAmt = PolicyPremium * (MGACommPercentage) / 100 + MGACommflatfee;
                                            var PolicyMGUCommAmt = PolicyPremium * (MGUCommPercentage) / 100 + MGUCommflatfee;
                                            var PolicyBrokerCommAmt = PolicyPremium * (BrokerCommPercentage) / 100 + BrokerCommflatfee;

                                            item["lux_mgacommission"] = PolicyMGACommAmt * 100 / PolicyPremium;
                                            item["lux_mgucommission"] = PolicyMGUCommAmt * 100 / PolicyPremium;
                                            item["lux_brokercommission"] = PolicyBrokerCommAmt * 100 / PolicyPremium;
                                            item["lux_totalcommission"] = (PolicyMGACommAmt * 100 / PolicyPremium) + (PolicyBrokerCommAmt * 100 / PolicyPremium);
                                        }
                                        else
                                        {
                                            item["lux_mgacommission"] = 0M;
                                            item["lux_mgucommission"] = 0M;
                                            item["lux_brokercommission"] = 0M;
                                            item["lux_totalcommission"] = 0M;
                                        }

                                        var PolicyTaxRate = 0M;
                                        if (taxList.Entities.Count() > 0)
                                        {
                                            PolicyTaxRate = taxList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_areweraisingthetaxorjustreporting").Value != 972970003 && (x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == tradeCover || x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == 972970005)).Sum(x => x.Attributes.Contains("lux_taxpercentage") ? x.GetAttributeValue<decimal>("lux_taxpercentage") : 0);
                                            item["lux_totaltax"] = PolicyTaxRate;
                                        }
                                        else
                                        {
                                            item["lux_totaltax"] = 0M;
                                        }

                                        var PolicyPercentageFee = 0M;
                                        var PolicyFlatFee = 0M;

                                        if (FeeList.Entities.Count() > 0)
                                        {
                                            PolicyPercentageFee = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == tradeCover && x.GetAttributeValue<OptionSetValue>("lux_pisection").Value != 972970005).Sum(x => x.Attributes.Contains("lux_feepercentage") ? x.GetAttributeValue<decimal>("lux_feepercentage") : 0);
                                            PolicyFlatFee = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == tradeCover && x.GetAttributeValue<OptionSetValue>("lux_pisection").Value != 972970005).Sum(x => x.Attributes.Contains("lux_feeamount") ? x.GetAttributeValue<Money>("lux_feeamount").Value : 0);
                                            var PolicyFeeAmt = (PolicyPremium * PolicyPercentageFee / 100) + PolicyFlatFee;

                                            if (ApplicationType == 972970001)
                                            {
                                                item["lux_policyfee"] = new Money(PolicyFeeAmt);
                                            }
                                            else if (ApplicationType == 972970002)
                                            {
                                                item["lux_mtapolicyfee"] = new Money(PolicyFeeAmt);
                                            }
                                        }
                                        else
                                        {
                                            if (ApplicationType == 972970001)
                                            {
                                                item["lux_policyfee"] = new Money(0);
                                            }
                                            else if (ApplicationType == 972970002)
                                            {
                                                item["lux_mtapolicyfee"] = new Money(0);
                                            }
                                        }

                                        organizationService.Update(item);
                                    }
                                }

                                var quoteOptionFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                      <entity name='lux_subscribequoteoption'>
                                                        <attribute name='lux_subscribequoteoptionid' />
                                                        <attribute name='lux_name' />
                                                        <attribute name='createdon' />
                                                        <order attribute='lux_name' descending='false' />
                                                        <filter type='and'>
                                                          <condition attribute='statecode' operator='eq' value='0' />
                                                          <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subscribeQuote.Id}' />
                                                        </filter>
                                                      </entity>
                                                    </fetch>";

                                var quoteOptonList = organizationService.RetrieveMultiple(new FetchExpression(quoteOptionFetch));
                                foreach (var item in quoteOptonList.Entities)
                                {
                                    var tradefetch11 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_subscribepisectionpremium'>
                                            <attribute name='lux_sectionreference' />
                                            <attribute name='lux_technicalpremium' />
                                            <attribute name='lux_ratingfigures' />
                                            <attribute name='lux_ratedeviation' />
                                            <attribute name='lux_policypremium' />
                                            <attribute name='lux_policypremiumbeforetax' />
                                            <attribute name='lux_justificaiton' />
                                            <attribute name='lux_loaddiscount' />
                                            <attribute name='lux_policyfee' />
                                            <attribute name='lux_brokercommissionamount' />
                                            <attribute name='lux_mgacommissionamount' />
                                            <attribute name='lux_mgucommissionamount' />   
                                            <attribute name='lux_totaltaxamount' />
                                            <attribute name='lux_comment' />
                                            <attribute name='lux_section' />
                                            <attribute name='lux_subscribepisectionpremiumid' />
                                            <order attribute='lux_sectionreference' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <condition attribute='lux_subscribequoteoption' operator='eq' uiname='' uitype='lux_subscribequoteoption' value='{item.Id}' />
                                            </filter>
                                          </entity>
                                        </fetch>";

                                    var tradeList11 = organizationService.RetrieveMultiple(new FetchExpression(tradefetch11)).Entities;

                                    Entity totalRow = organizationService.Retrieve("lux_subscribepisectionpremium", tradeList11.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970006).Id, new ColumnSet(false));
                                    totalRow["lux_ratingfigures"] = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_ratingfigures") ? x.GetAttributeValue<Money>("lux_ratingfigures").Value : 0);

                                    var technicalPrem = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_technicalpremium") ? x.GetAttributeValue<Money>("lux_technicalpremium").Value : 0);
                                    var policyPrem = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_policypremium") ? x.GetAttributeValue<Money>("lux_policypremium").Value : 0);
                                    var policyPrembeforeTax = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_policypremiumbeforetax") ? x.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0);

                                    var BrokerCommission = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_brokercommissionamount") ? x.GetAttributeValue<Money>("lux_brokercommissionamount").Value : 0);
                                    var MGACommission = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_mgacommissionamount") ? x.GetAttributeValue<Money>("lux_mgacommissionamount").Value : 0);
                                    var MGUCommission = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_mgucommissionamount") ? x.GetAttributeValue<Money>("lux_mgucommissionamount").Value : 0);
                                    var TaxAmount = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_totaltaxamount") ? x.GetAttributeValue<Money>("lux_totaltaxamount").Value : 0);
                                    var Fee = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_policyfee") ? x.GetAttributeValue<Money>("lux_policyfee").Value : 0);

                                    var PolicyPercentageFeeOverall = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == 972970005).Sum(x => x.Attributes.Contains("lux_feepercentage") ? x.GetAttributeValue<decimal>("lux_feepercentage") : 0);
                                    var PolicyFlatFeeOverall = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_pisection").Value == 972970005).Sum(x => x.Attributes.Contains("lux_feeamount") ? x.GetAttributeValue<Money>("lux_feeamount").Value : 0);
                                    var PolicyFeeAmtOverall = (policyPrembeforeTax * PolicyPercentageFeeOverall / 100) + PolicyFlatFeeOverall;

                                    if (policyPrembeforeTax != 0)
                                    {
                                        totalRow["lux_technicalpremium"] = new Money(technicalPrem);
                                        totalRow["lux_loaddiscount"] = (policyPrem * 100 / technicalPrem) - 100;
                                        totalRow["lux_justificaiton"] = "NA";
                                        totalRow["lux_brokercommission"] = BrokerCommission * 100 / policyPrembeforeTax;
                                        totalRow["lux_mgacommission"] = MGACommission * 100 / policyPrembeforeTax;
                                        totalRow["lux_mgucommission"] = MGUCommission * 100 / policyPrembeforeTax;
                                        totalRow["lux_totaltax"] = TaxAmount * 100 / policyPrembeforeTax;
                                        if (ApplicationType == 972970001)
                                        {
                                            totalRow["lux_policyfee"] = new Money(Fee + PolicyFeeAmtOverall);
                                        }
                                        else if (ApplicationType == 972970002)
                                        {
                                            totalRow["lux_mtapolicyfee"] = new Money(Fee + PolicyFeeAmtOverall);
                                        }
                                        organizationService.Update(totalRow);
                                    }
                                }
                            }
                        }
                    }

                    //Entity PremiumData = organizationService.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));
                    //var PremiumQuoteOption = organizationService.Retrieve("lux_subscribequoteoption", PremiumData.GetAttributeValue<EntityReference>("lux_subscribequoteoption").Id, new ColumnSet(true));
                    if (SelectedQuoteOptionId != "")
                    {
                        var subscribeQuote = organizationService.Retrieve("lux_subscribepiquote", new Guid(subscribeQuoteId), new ColumnSet("transactioncurrencyid", "lux_applicationtype"));
                        var ApplicationType = subscribeQuote.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value;
                        var selectedQuoteOption = organizationService.Retrieve("lux_subscribequoteoption", new Guid(SelectedQuoteOptionId), new ColumnSet(true));

                        var tradefetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_subscribepisectionpremium'>
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
                                            <attribute name='lux_subscribepisectionpremiumid' />
                                            <order attribute='lux_sectionreference' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <condition attribute='lux_subscribequoteoption' operator='eq' uiname='' uitype='lux_subscribequoteoption' value='{selectedQuoteOption.Id}' />
                                            </filter>
                                          </entity>
                                        </fetch>";

                        var tradeList1 = organizationService.RetrieveMultiple(new FetchExpression(tradefetch1)).Entities;
                        var Count = tradeList1.Count();

                        var TechnicalPremium = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_technicalpremium") ? x.GetAttributeValue<Money>("lux_technicalpremium").Value : 0);
                        var PolicyPremium = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_policypremiumbeforetax") ? x.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0);
                        var BrokerCommission = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_brokercommissionamount") ? x.GetAttributeValue<Money>("lux_brokercommissionamount").Value : 0);
                        var MGACommission = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_mgacommissionamount") ? x.GetAttributeValue<Money>("lux_mgacommissionamount").Value : 0);
                        var MGUCommission = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_mgucommissionamount") ? x.GetAttributeValue<Money>("lux_mgucommissionamount").Value : 0);
                        var TaxAmount = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_totaltaxamount") ? x.GetAttributeValue<Money>("lux_totaltaxamount").Value : 0);
                        var Tax = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_totaltax") ? x.GetAttributeValue<decimal>("lux_totaltax") : 0);
                        var Fee = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970006).Sum(x => x.Attributes.Contains("lux_policyfee") ? x.GetAttributeValue<Money>("lux_policyfee").Value : 0);

                        if (PolicyPremium != 0)
                        {
                            subscribeQuote["lux_technicalpremiumbeforetax"] = new Money(TechnicalPremium);
                            subscribeQuote["lux_policypremiumbeforetax"] = new Money(PolicyPremium);

                            selectedQuoteOption["lux_technicalpremiumbeforetax"] = new Money(TechnicalPremium);
                            selectedQuoteOption["lux_policypremiumbeforetax"] = new Money(PolicyPremium);
                            selectedQuoteOption["lux_policybrokercommissionamount"] = BrokerCommission;
                            selectedQuoteOption["lux_policybrokercommissionpercentage"] = BrokerCommission * 100 / PolicyPremium;
                            selectedQuoteOption["lux_policymgacommissionpercentage"] = MGACommission * 100 / PolicyPremium;
                            selectedQuoteOption["lux_policyaciesmgucommissionpercentage"] = MGUCommission * 100 / PolicyPremium;
                            selectedQuoteOption["lux_policytotaltaxamount"] = new Money(TaxAmount);
                            selectedQuoteOption["lux_policytotaltax"] = TaxAmount * 100 / PolicyPremium;
                            if (ApplicationType == 972970001)
                            {
                                selectedQuoteOption["lux_policyfee"] = new Money(Fee);
                            }
                        }
                        else
                        {
                            subscribeQuote["lux_technicalpremiumbeforetax"] = new Money(0);
                            subscribeQuote["lux_policypremiumbeforetax"] = new Money(0);

                            selectedQuoteOption["lux_technicalpremiumbeforetax"] = new Money(0);
                            selectedQuoteOption["lux_policypremiumbeforetax"] = new Money(0);
                            selectedQuoteOption["lux_policybrokercommissionamount"] = BrokerCommission;
                            selectedQuoteOption["lux_policybrokercommissionpercentage"] = null;
                            selectedQuoteOption["lux_policymgacommissionpercentage"] = null;
                            selectedQuoteOption["lux_policyaciesmgucommissionpercentage"] = null;
                            selectedQuoteOption["lux_policytotaltaxamount"] = new Money(TaxAmount);
                            selectedQuoteOption["lux_policytotaltax"] = null;
                            selectedQuoteOption["lux_policyfee"] = new Money(0);
                        }

                        if (ApplicationType == 972970002) //MTA
                        {
                            var MTAPolicyPremium = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_mtapolicypremiumbeforetax") ? x.GetAttributeValue<Money>("lux_mtapolicypremiumbeforetax").Value : 0);
                            var MTATotalTax = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970006).Sum(x => x.Attributes.Contains("lux_mtatotaltaxamount") ? x.GetAttributeValue<Money>("lux_mtatotaltaxamount").Value : 0);
                            var MTAFee = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970006).Sum(x => x.Attributes.Contains("lux_mtapolicyfee") ? x.GetAttributeValue<Money>("lux_mtapolicyfee").Value : 0);

                            if (PolicyPremium != 0)
                            {
                                subscribeQuote["lux_mtapolicypremiumbeforetax"] = new Money(MTAPolicyPremium);
                                subscribeQuote["lux_mtatotaltaxamount"] = new Money(MTATotalTax);
                                subscribeQuote["lux_mtapolicyfee"] = new Money(MTAFee);
                            }
                            else
                            {
                                subscribeQuote["lux_mtapolicypremiumbeforetax"] = new Money(0);
                                subscribeQuote["lux_mtatotaltaxamount"] = new Money(0);
                                subscribeQuote["lux_mtapolicyfee"] = new Money(0);
                            }
                        }

                        var brokerFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_subscribebrokersagent'>
                                                <attribute name='lux_percentageorflatfee' />
                                                <attribute name='lux_percentage' />
                                                <attribute name='lux_commissonamount' />
                                                <attribute name='lux_pisection' />
                                                <attribute name='lux_companytype' />
                                                <attribute name='lux_company' />
                                                <attribute name='lux_commissionamountforsignedshare' />
                                                <attribute name='lux_commissonamountforsubgrid' />
                                                <attribute name='lux_subscribebrokersagentid' />
                                                <order attribute='lux_companytype' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_subscribepiquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subscribeQuote.Id}' />
                                                  <condition attribute='lux_pisection' operator='ne' value='972970005' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                        var brokerList = organizationService.RetrieveMultiple(new FetchExpression(brokerFetch)).Entities;
                        foreach (var item in brokerList)
                        {
                            var subscribeSection = item.GetAttributeValue<OptionSetValue>("lux_pisection").Value;
                            var SectionalPolicyPremium = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == subscribeSection).Sum(x => x.Attributes.Contains("lux_policypremiumbeforetax") ? x.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0);
                            item["lux_sectionalpolicypremiumbeforetax"] = new Money(SectionalPolicyPremium);
                            item["transactioncurrencyid"] = new EntityReference("transactioncurrency", subscribeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            organizationService.Update(item);
                        }

                        var taxFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_pisection' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_areweraisingthetaxorjustreporting' />
                                                    <attribute name='lux_subscribequotetaxtypeid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subscribeQuote.Id}' />
                                                      <condition attribute='lux_pisection' operator='ne' value='972970005' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                        var taxList = organizationService.RetrieveMultiple(new FetchExpression(taxFetch)).Entities;
                        foreach (var item in taxList)
                        {
                            var subscribeSection = item.GetAttributeValue<OptionSetValue>("lux_pisection").Value;
                            var SectionalPolicyPremium = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == subscribeSection).Sum(x => x.Attributes.Contains("lux_policypremiumbeforetax") ? x.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0);
                            item["lux_sectionalpolicypremiumbeforetax"] = new Money(SectionalPolicyPremium);
                            item["transactioncurrencyid"] = new EntityReference("transactioncurrency", subscribeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            organizationService.Update(item);
                        }

                        var feeFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribefeetable'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_pisection' />
                                                    <attribute name='lux_feepercentage' />
                                                    <attribute name='lux_feeamount' />
                                                    <attribute name='lux_feebasis' />
                                                    <attribute name='lux_subscribefeetableid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subscribeQuote.Id}' />
                                                      <condition attribute='lux_pisection' operator='ne' value='972970005' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                        var feeFetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribefeetable'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_pisection' />
                                                    <attribute name='lux_feepercentage' />
                                                    <attribute name='lux_feeamount' />
                                                    <attribute name='lux_feebasis' />
                                                    <attribute name='lux_subscribefeetableid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subscribeQuote.Id}' />
                                                      <condition attribute='lux_pisection' operator='eq' value='972970005' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                        var feeList = organizationService.RetrieveMultiple(new FetchExpression(feeFetch)).Entities;
                        foreach (var item in feeList)
                        {
                            var subscribeSection = item.GetAttributeValue<OptionSetValue>("lux_pisection").Value;
                            var SectionalPolicyPremium = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == subscribeSection).Sum(x => x.Attributes.Contains("lux_policypremiumbeforetax") ? x.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0);
                            item["lux_sectionalpolicypremiumbeforetax"] = new Money(SectionalPolicyPremium);
                            item["transactioncurrencyid"] = new EntityReference("transactioncurrency", subscribeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            organizationService.Update(item);
                        }

                        var feeList1 = organizationService.RetrieveMultiple(new FetchExpression(feeFetch1)).Entities;
                        foreach (var item in feeList1)
                        {
                            item["transactioncurrencyid"] = new EntityReference("transactioncurrency", subscribeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            organizationService.Update(item);
                        }

                        if (PolicyPremium == 0)
                        {
                            var TotalBrokerCommission = 0M;
                            var TotalMGACommission = 0M;
                            var TotalMGUCommission = 0M;
                            var TotalTax = 0M;

                            var brokerFetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_subscribebrokersagent'>
                                                <attribute name='lux_percentageorflatfee' />
                                                <attribute name='lux_percentage' />
                                                <attribute name='lux_commissonamount' />
                                                <attribute name='lux_pisection' />
                                                <attribute name='lux_companytype' />
                                                <attribute name='lux_company' />
                                                <attribute name='lux_commissionamountforsignedshare' />
                                                <attribute name='lux_commissonamountforsubgrid' />
                                                <attribute name='lux_subscribebrokersagentid' />
                                                <order attribute='lux_companytype' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_subscribepiquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subscribeQuote.Id}' />
                                                  <condition attribute='lux_pisection' operator='eq' value='972970005' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                            var taxFetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_pisection' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_areweraisingthetaxorjustreporting' />
                                                    <attribute name='lux_subscribequotetaxtypeid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subscribeQuote.Id}' />
                                                      <condition attribute='lux_pisection' operator='eq' value='972970005' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                            foreach (var item in organizationService.RetrieveMultiple(new FetchExpression(brokerFetch1)).Entities)
                            {
                                if (item.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970001)
                                {
                                    TotalMGACommission += item.Attributes.Contains("lux_percentage") ? item.GetAttributeValue<decimal>("lux_percentage") : 0;
                                }
                                if (item.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970002)
                                {
                                    TotalMGUCommission += item.Attributes.Contains("lux_percentage") ? item.GetAttributeValue<decimal>("lux_percentage") : 0;
                                }
                                if (item.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970003)
                                {
                                    TotalBrokerCommission += item.Attributes.Contains("lux_percentage") ? item.GetAttributeValue<decimal>("lux_percentage") : 0;
                                }
                            }

                            foreach (var item in organizationService.RetrieveMultiple(new FetchExpression(taxFetch1)).Entities)
                            {
                                if (item.Attributes.Contains("lux_taxpercentage"))
                                {
                                    TotalTax += item.Attributes.Contains("lux_taxpercentage") ? item.GetAttributeValue<decimal>("lux_taxpercentage") : 0;
                                }
                            }

                            selectedQuoteOption["lux_policybrokercommissionpercentage"] = TotalBrokerCommission;
                            selectedQuoteOption["lux_policymgacommissionpercentage"] = TotalMGACommission;
                            selectedQuoteOption["lux_policyaciesmgucommissionpercentage"] = TotalMGUCommission;
                            selectedQuoteOption["lux_policytotaltax"] = TotalTax;
                        }

                        organizationService.Update(subscribeQuote);
                        organizationService.Update(selectedQuoteOption);
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
