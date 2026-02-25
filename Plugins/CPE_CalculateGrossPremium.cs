using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace Acies_Customization.Plugins
{
    public class CPE_CalculateGrossPremium : IPlugin
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

                    var cpeQuoteId = "";
                    var SelectedQuoteOptionId = "";
                    if (entity.LogicalName == "lux_contractorsplantandequipmentquotepremui")
                    {
                        Entity PremiumRow = organizationService.Retrieve("lux_contractorsplantandequipmentquotepremui", entity.Id, new ColumnSet(true));

                        if (PremiumRow.Attributes.Contains("lux_phoenixquoteoption"))
                        {
                            var QuoteOption = organizationService.Retrieve("lux_phoenixquoteoption", PremiumRow.GetAttributeValue<EntityReference>("lux_phoenixquoteoption").Id, new ColumnSet(true));
                            var cpeQuote = organizationService.Retrieve("lux_contractorsplantandequipmentquote", QuoteOption.GetAttributeValue<EntityReference>("lux_contractorsplantandequipmentquote").Id, new ColumnSet("lux_quoteoptionselected", "lux_applicationtype", "transactioncurrencyid"));
                            cpeQuoteId = cpeQuote.Id.ToString();
                            SelectedQuoteOptionId = cpeQuote.GetAttributeValue<EntityReference>("lux_quoteoptionselected").Id.ToString();
                            var ApplicationType = cpeQuote.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value;

                            var tradefetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_contractorsplantandequipmentquotepremui'>
                                            <attribute name='lux_sectionreference' />
                                            <attribute name='lux_technicalpremium' />
                                            <attribute name='lux_ratingfigures' />
                                            <attribute name='lux_ratedeviation' />
                                            <attribute name='lux_policypremiumbeforetax' />
                                            <attribute name='lux_justificaiton' />
                                            <attribute name='lux_loaddiscount' />
                                            <attribute name='lux_comment' />
                                            <attribute name='lux_section' />
                                            <attribute name='lux_contractorsplantandequipmentquotepremuiid' />
                                            <order attribute='lux_sectionreference' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <condition attribute='lux_phoenixquoteoption' operator='eq' uiname='' uitype='lux_phoenixquoteoption' value='{QuoteOption.Id}' />
                                            </filter>
                                          </entity>
                                        </fetch>";
                            var tradeList1 = organizationService.RetrieveMultiple(new FetchExpression(tradefetch1));

                            var brokerfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                      <entity name='lux_cpebrokeragent'>
                                                        <attribute name='lux_cpesection' />
                                                        <attribute name='lux_percentageorflatfee' />
                                                        <attribute name='lux_broker' />
                                                        <attribute name='lux_percentage' />
                                                        <attribute name='lux_companytype' />
                                                        <attribute name='lux_commissonamount' />
                                                        <attribute name='lux_cpebrokeragentid' />
                                                        <order attribute='lux_broker' descending='false' />
                                                        <filter type='and'>
                                                          <condition attribute='statecode' operator='eq' value='0' />
                                                          <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                        </filter>
                                                      </entity>
                                                    </fetch>";
                            var brokerList = organizationService.RetrieveMultiple(new FetchExpression(brokerfetch));

                            var Taxfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_cpequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_section' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_taxprofile' />
                                                    <attribute name='lux_areweraisingthetaxorjustreporting' />
                                                    <attribute name='lux_cpequotetaxtypeid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                            var taxList = organizationService.RetrieveMultiple(new FetchExpression(Taxfetch));

                            var Feefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_phoenixfeetable'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_section' />
                                                    <attribute name='lux_feepercentage' />
                                                    <attribute name='lux_feeamount' />
                                                    <attribute name='lux_feebasis' />
                                                    <attribute name='lux_feetype' />
                                                    <attribute name='lux_phoenixfeetableid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                            var FeeList = organizationService.RetrieveMultiple(new FetchExpression(Feefetch));

                            foreach (var item in tradeList1.Entities)
                            {
                                var tradeCover = item.Contains("lux_section") ? item.GetAttributeValue<OptionSetValue>("lux_section").Value + 2 : 0;
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
                                        if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                        {
                                            MGACommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        }
                                        else
                                        {
                                            MGACommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        }

                                        if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                        {
                                            MGACommflatfee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);
                                        }
                                        else
                                        {
                                            MGACommflatfee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);
                                        }

                                        if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                        {
                                            MGUCommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        }
                                        else
                                        {
                                            MGUCommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        }

                                        if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                        {
                                            MGUCommflatfee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);
                                        }
                                        else
                                        {
                                            MGUCommflatfee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);
                                        }

                                        var leadBroker = 0M;
                                        var CoBroker = 0M;
                                        var LloydBroker = 0M;
                                        var LocalBroker = 0M;

                                        if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970003 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                        {
                                            leadBroker = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970003 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        }
                                        else
                                        {
                                            leadBroker = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970003 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        }

                                        if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970004 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                        {
                                            CoBroker = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970004 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        }
                                        else
                                        {
                                            CoBroker = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970004 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        }

                                        if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970005 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                        {
                                            LloydBroker = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970005 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        }
                                        else
                                        {
                                            LloydBroker = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970005 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        }

                                        if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970006 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                        {
                                            LocalBroker = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970006 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        }
                                        else
                                        {
                                            LocalBroker = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970006 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        }

                                        BrokerCommPercentage = leadBroker + CoBroker + LloydBroker + LocalBroker;



                                        //if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                        //{
                                        //    BrokerCommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        //}
                                        //else
                                        //{
                                        //    BrokerCommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        //}

                                        var leadBrokerFee = 0M;
                                        var CoBrokerFee = 0M;
                                        var LloydBrokerFee = 0M;
                                        var LocalBrokerFee = 0M;

                                        if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970003 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                        {
                                            leadBrokerFee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970003 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        }
                                        else
                                        {
                                            leadBrokerFee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970003 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        }

                                        if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970004 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                        {
                                            CoBrokerFee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970004 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        }
                                        else
                                        {
                                            CoBrokerFee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970004 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        }

                                        if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970005 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                        {
                                            LloydBrokerFee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970005 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        }
                                        else
                                        {
                                            LloydBrokerFee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970005 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        }

                                        if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970006 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                        {
                                            LocalBrokerFee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970006 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        }
                                        else
                                        {
                                            LocalBrokerFee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970006 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                        }

                                        BrokerCommflatfee = leadBrokerFee + CoBrokerFee + LloydBrokerFee + LocalBrokerFee;

                                        //if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                        //{
                                        //    BrokerCommflatfee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);
                                        //}
                                        //else
                                        //{
                                        //    BrokerCommflatfee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);
                                        //}

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
                                        if (taxList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_areweraisingthetaxorjustreporting").Value != 972970003 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == tradeCover).Count() > 0)
                                        {
                                            var tradetaxdata = taxList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_areweraisingthetaxorjustreporting").Value != 972970003 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == tradeCover);
                                            PolicyTaxRate += tradetaxdata.Sum(x => x.Attributes.Contains("lux_taxpercentage") ? x.GetAttributeValue<decimal>("lux_taxpercentage") : 0);

                                            foreach (var item1 in taxList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_areweraisingthetaxorjustreporting").Value != 972970003 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970001))
                                            {
                                                if (tradetaxdata.Where(x => x.GetAttributeValue<EntityReference>("lux_taxprofile").Id == item1.GetAttributeValue<EntityReference>("lux_taxprofile").Id).Count() == 0)
                                                {
                                                    PolicyTaxRate += item1.Attributes.Contains("lux_taxpercentage") ? item1.GetAttributeValue<decimal>("lux_taxpercentage") : 0;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            PolicyTaxRate += taxList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_areweraisingthetaxorjustreporting").Value != 972970003 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970001).Sum(x => x.Attributes.Contains("lux_taxpercentage") ? x.GetAttributeValue<decimal>("lux_taxpercentage") : 0);
                                        }

                                        item["lux_totaltax"] = PolicyTaxRate;
                                    }

                                    if (FeeList.Entities.Count() > 0)
                                    {
                                        //if (FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == tradeCover).Count() > 0)
                                        //{
                                        //    var tradefeedata = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == tradeCover);
                                        //    PolicyPercentageFee += tradefeedata.Sum(x => x.Attributes.Contains("lux_feepercentage") ? x.GetAttributeValue<decimal>("lux_feepercentage") : 0);

                                        //    foreach (var item1 in FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970001))
                                        //    {
                                        //        if (tradefeedata.Where(x => x.GetAttributeValue<EntityReference>("lux_feetype").Id == item1.GetAttributeValue<EntityReference>("lux_feetype").Id).Count() == 0)
                                        //        {
                                        //            PolicyPercentageFee += item1.Attributes.Contains("lux_feepercentage") ? item1.GetAttributeValue<decimal>("lux_feepercentage") : 0;
                                        //        }
                                        //    }
                                        //}
                                        //else
                                        //{
                                        //    PolicyPercentageFee = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970001).Sum(x => x.Attributes.Contains("lux_feepercentage") ? x.GetAttributeValue<decimal>("lux_feepercentage") : 0);
                                        //}

                                        //if (FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == tradeCover).Count() > 0)
                                        //{
                                        //    var tradefeedata = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == tradeCover);
                                        //    PolicyFlatFee += tradefeedata.Sum(x => x.Attributes.Contains("lux_feepercentage") ? x.GetAttributeValue<decimal>("lux_feepercentage") : 0);

                                        //    foreach (var item1 in FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970001))
                                        //    {
                                        //        if (tradefeedata.Where(x => x.GetAttributeValue<EntityReference>("lux_feetype").Id == item1.GetAttributeValue<EntityReference>("lux_feetype").Id).Count() == 0)
                                        //        {
                                        //            PolicyFlatFee += item1.Attributes.Contains("lux_feepercentage") ? item1.GetAttributeValue<decimal>("lux_feepercentage") : 0;
                                        //        }
                                        //    }
                                        //}
                                        //else
                                        //{
                                        //    PolicyFlatFee = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970001).Sum(x => x.Attributes.Contains("lux_feepercentage") ? x.GetAttributeValue<decimal>("lux_feepercentage") : 0);
                                        //}

                                        PolicyPercentageFee = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == tradeCover && x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970001).Sum(x => x.Attributes.Contains("lux_feepercentage") ? x.GetAttributeValue<decimal>("lux_feepercentage") : 0);
                                        PolicyFlatFee = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == tradeCover && x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970001).Sum(x => x.Attributes.Contains("lux_feeamount") ? x.GetAttributeValue<Money>("lux_feeamount").Value : 0);
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
                                    item["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                    organizationService.Update(item);
                                }
                            }

                            var tradefetch11 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_contractorsplantandequipmentquotepremui'>
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
                                            <attribute name='lux_contractorsplantandequipmentquotepremuiid' />
                                            <order attribute='lux_sectionreference' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <condition attribute='lux_phoenixquoteoption' operator='eq' uiname='' uitype='lux_phoenixquoteoption' value='{QuoteOption.Id}' />
                                            </filter>
                                          </entity>
                                        </fetch>";

                            var tradeList11 = organizationService.RetrieveMultiple(new FetchExpression(tradefetch11)).Entities;

                            Entity totalRow = organizationService.Retrieve("lux_contractorsplantandequipmentquotepremui", tradeList11.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970013).Id, new ColumnSet(false));
                            totalRow["lux_ratingfigures"] = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_ratingfigures") ? x.GetAttributeValue<Money>("lux_ratingfigures").Value : 0);
                            totalRow["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            var technicalPrem = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_technicalpremium") ? x.GetAttributeValue<Money>("lux_technicalpremium").Value : 0);
                            var policyPrem = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_policypremium") ? x.GetAttributeValue<Money>("lux_policypremium").Value : 0);
                            var policyPrembeforeTax = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_policypremiumbeforetax") ? x.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0);

                            var BrokerCommission = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_brokercommissionamount") ? x.GetAttributeValue<Money>("lux_brokercommissionamount").Value : 0);
                            var MGACommission = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_mgacommissionamount") ? x.GetAttributeValue<Money>("lux_mgacommissionamount").Value : 0);
                            var MGUCommission = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_mgucommissionamount") ? x.GetAttributeValue<Money>("lux_mgucommissionamount").Value : 0);
                            var TaxAmount = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_totaltaxamount") ? x.GetAttributeValue<Money>("lux_totaltaxamount").Value : 0);
                            var Fee = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_policyfee") ? x.GetAttributeValue<Money>("lux_policyfee").Value : 0);

                            var PolicyPercentageFeeOverall = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970001).Sum(x => x.Attributes.Contains("lux_feepercentage") ? x.GetAttributeValue<decimal>("lux_feepercentage") : 0);
                            var PolicyFlatFeeOverall = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970001).Sum(x => x.Attributes.Contains("lux_feeamount") ? x.GetAttributeValue<Money>("lux_feeamount").Value : 0);
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
                        Entity cpeEntity = new Entity();
                        if (entity.LogicalName == "lux_cpebrokeragent")
                        {
                            cpeEntity = organizationService.Retrieve("lux_cpebrokeragent", entity.Id, new ColumnSet(true));
                        }
                        else if (entity.LogicalName == "lux_cpequotetaxtype")
                        {
                            cpeEntity = organizationService.Retrieve("lux_cpequotetaxtype", entity.Id, new ColumnSet(true));
                        }
                        else if (entity.LogicalName == "lux_phoenixfeetable")
                        {
                            cpeEntity = organizationService.Retrieve("lux_phoenixfeetable", entity.Id, new ColumnSet(true));
                        }

                        if (cpeEntity.Attributes.Contains("lux_contractorsplantandequipmentquote"))
                        {
                            var cpeQuote = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeEntity.GetAttributeValue<EntityReference>("lux_contractorsplantandequipmentquote").Id, new ColumnSet(true));
                            cpeQuoteId = cpeQuote.Id.ToString();
                            SelectedQuoteOptionId = cpeQuote.Contains("lux_quoteoptionselected") ? cpeQuote.GetAttributeValue<EntityReference>("lux_quoteoptionselected").Id.ToString() : "";
                            var ApplicationType = cpeQuote.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value;

                            var cpePremium = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_contractorsplantandequipmentquotepremui'>
                                            <attribute name='lux_sectionreference' />
                                            <attribute name='lux_technicalpremium' />
                                            <attribute name='lux_ratingfigures' />
                                            <attribute name='lux_ratedeviation' />
                                            <attribute name='lux_policypremiumbeforetax' />
                                            <attribute name='lux_justificaiton' />
                                            <attribute name='lux_loaddiscount' />
                                            <attribute name='lux_comment' />
                                            <attribute name='lux_section' />
                                            <attribute name='lux_contractorsplantandequipmentquotepremuiid' />
                                            <order attribute='lux_sectionreference' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                            </filter>
                                            <link-entity name='lux_phoenixquoteoption' from='lux_phoenixquoteoptionid' to='lux_phoenixquoteoption' link-type='inner' alias='ab'>
                                                <filter type='and'>
                                                  <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                </filter>
                                            </link-entity>
                                          </entity>
                                        </fetch>";

                            var cpePremiumLst = organizationService.RetrieveMultiple(new FetchExpression(cpePremium));

                            var brokerfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                      <entity name='lux_cpebrokeragent'>
                                                        <attribute name='lux_cpesection' />
                                                        <attribute name='lux_percentageorflatfee' />
                                                        <attribute name='lux_broker' />
                                                        <attribute name='lux_percentage' />
                                                        <attribute name='lux_companytype' />
                                                        <attribute name='lux_commissonamount' />
                                                        <attribute name='lux_cpebrokeragentid' />
                                                        <order attribute='lux_broker' descending='false' />
                                                        <filter type='and'>
                                                          <condition attribute='statecode' operator='eq' value='0' />
                                                          <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                        </filter>
                                                      </entity>
                                                    </fetch>";
                            if (context.MessageName == "Delete" && entity.LogicalName == "lux_cpebrokeragent")
                            {
                                brokerfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                      <entity name='lux_cpebrokeragent'>
                                                        <attribute name='lux_cpesection' />
                                                        <attribute name='lux_percentageorflatfee' />
                                                        <attribute name='lux_broker' />
                                                        <attribute name='lux_percentage' />
                                                        <attribute name='lux_companytype' />
                                                        <attribute name='lux_commissonamount' />
                                                        <attribute name='lux_cpebrokeragentid' />
                                                        <order attribute='lux_broker' descending='false' />
                                                        <filter type='and'>
                                                          <condition attribute='statecode' operator='eq' value='0' />
                                                          <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                          <condition attribute='lux_cpebrokeragentid' operator='ne' uiname='' uitype='lux_cpebrokeragent' value='{entity.Id}' />
                                                        </filter>
                                                      </entity>
                                                    </fetch>";
                            }

                            var brokerList = organizationService.RetrieveMultiple(new FetchExpression(brokerfetch));

                            var Taxfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_cpequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_section' />
                                                    <attribute name='lux_taxprofile' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_areweraisingthetaxorjustreporting' />
                                                    <attribute name='lux_cpequotetaxtypeid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";
                            if (context.MessageName == "Delete" && entity.LogicalName == "lux_cpequotetaxtype")
                            {
                                Taxfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_cpequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_section' />
                                                    <attribute name='lux_taxprofile' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_areweraisingthetaxorjustreporting' />
                                                    <attribute name='lux_cpequotetaxtypeid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                      <condition attribute='lux_cpequotetaxtypeid' operator='ne' uiname='' uitype='lux_cpequotetaxtype' value='{entity.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";
                            }
                            var taxList = organizationService.RetrieveMultiple(new FetchExpression(Taxfetch));

                            var Feefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_phoenixfeetable'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_section' />
                                                    <attribute name='lux_feepercentage' />
                                                    <attribute name='lux_feeamount' />
                                                    <attribute name='lux_feebasis' />
                                                    <attribute name='lux_feetype' />
                                                    <attribute name='lux_phoenixfeetableid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";
                            if (context.MessageName == "Delete" && entity.LogicalName == "lux_phoenixfeetable")
                            {
                                Feefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_phoenixfeetable'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_section' />
                                                    <attribute name='lux_feepercentage' />
                                                    <attribute name='lux_feeamount' />
                                                    <attribute name='lux_feetype' />
                                                    <attribute name='lux_feebasis' />
                                                    <attribute name='lux_phoenixfeetableid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                      <condition attribute='lux_phoenixfeetableid' operator='ne' uiname='' uitype='lux_phoenixfeetable' value='{entity.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";
                            }

                            var FeeList = organizationService.RetrieveMultiple(new FetchExpression(Feefetch));

                            if (cpePremiumLst.Entities.Count > 0)
                            {
                                foreach (var item in cpePremiumLst.Entities)
                                {
                                    var tradeCover = item.Contains("lux_section") ? item.GetAttributeValue<OptionSetValue>("lux_section").Value + 2 : 0;
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
                                            //if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                            //{
                                            //    MGACommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            //}
                                            //else
                                            //{
                                            //    MGACommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            //}

                                            //if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                            //{
                                            //    MGACommflatfee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);
                                            //}
                                            //else
                                            //{
                                            //    MGACommflatfee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);
                                            //}

                                            //if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                            //{
                                            //    MGUCommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            //}
                                            //else
                                            //{
                                            //    MGUCommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            //}

                                            //if(brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() >0)
                                            //{
                                            //    MGUCommflatfee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);
                                            //}
                                            //else
                                            //{
                                            //    MGUCommflatfee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);
                                            //}

                                            //if(brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                            //{
                                            //    BrokerCommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            //}
                                            //else
                                            //{
                                            //    BrokerCommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            //}

                                            //if(brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() >0)
                                            //{
                                            //    BrokerCommflatfee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);
                                            //}
                                            //else
                                            //{
                                            //    BrokerCommflatfee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value != 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);
                                            //}

                                            if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                            {
                                                MGACommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            }
                                            else
                                            {
                                                MGACommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            }

                                            if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                            {
                                                MGACommflatfee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);
                                            }
                                            else
                                            {
                                                MGACommflatfee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);
                                            }

                                            if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                            {
                                                MGUCommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            }
                                            else
                                            {
                                                MGUCommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            }

                                            if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                            {
                                                MGUCommflatfee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);
                                            }
                                            else
                                            {
                                                MGUCommflatfee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);
                                            }

                                            var leadBroker = 0M;
                                            var CoBroker = 0M;
                                            var LloydBroker = 0M;
                                            var LocalBroker = 0M;

                                            if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970003 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                            {
                                                leadBroker = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970003 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            }
                                            else
                                            {
                                                leadBroker = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970003 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            }

                                            if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970004 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                            {
                                                CoBroker = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970004 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            }
                                            else
                                            {
                                                CoBroker = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970004 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            }

                                            if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970005 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                            {
                                                LloydBroker = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970005 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            }
                                            else
                                            {
                                                LloydBroker = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970005 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            }

                                            if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970006 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                            {
                                                LocalBroker = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970006 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            }
                                            else
                                            {
                                                LocalBroker = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970006 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            }

                                            BrokerCommPercentage = leadBroker + CoBroker + LloydBroker + LocalBroker;


                                            var leadBrokerFee = 0M;
                                            var CoBrokerFee = 0M;
                                            var LloydBrokerFee = 0M;
                                            var LocalBrokerFee = 0M;

                                            if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970003 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                            {
                                                leadBrokerFee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970003 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            }
                                            else
                                            {
                                                leadBrokerFee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970003 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            }

                                            if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970004 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                            {
                                                CoBrokerFee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970004 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            }
                                            else
                                            {
                                                CoBrokerFee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970004 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            }

                                            if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970005 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                            {
                                                LloydBrokerFee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970005 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            }
                                            else
                                            {
                                                LloydBrokerFee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970005 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            }

                                            if (brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970006 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Count() > 0)
                                            {
                                                LocalBrokerFee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970006 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            }
                                            else
                                            {
                                                LocalBrokerFee = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_companytype").Value == 972970006 && x.GetAttributeValue<OptionSetValue>("lux_cpesection").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0);
                                            }

                                            BrokerCommflatfee = leadBrokerFee + CoBrokerFee + LloydBrokerFee + LocalBrokerFee;

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
                                            if (taxList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_areweraisingthetaxorjustreporting").Value != 972970003 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == tradeCover).Count() > 0)
                                            {
                                                var tradetaxdata = taxList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_areweraisingthetaxorjustreporting").Value != 972970003 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == tradeCover);
                                                PolicyTaxRate += tradetaxdata.Sum(x => x.Attributes.Contains("lux_taxpercentage") ? x.GetAttributeValue<decimal>("lux_taxpercentage") : 0);

                                                foreach (var item1 in taxList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_areweraisingthetaxorjustreporting").Value != 972970003 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970001))
                                                {
                                                    if (tradetaxdata.Where(x => x.GetAttributeValue<EntityReference>("lux_taxprofile").Id == item1.GetAttributeValue<EntityReference>("lux_taxprofile").Id).Count() == 0)
                                                    {
                                                        PolicyTaxRate += item1.Attributes.Contains("lux_taxpercentage") ? item1.GetAttributeValue<decimal>("lux_taxpercentage") : 0;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                PolicyTaxRate = taxList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_areweraisingthetaxorjustreporting").Value != 972970003 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970001).Sum(x => x.Attributes.Contains("lux_taxpercentage") ? x.GetAttributeValue<decimal>("lux_taxpercentage") : 0);
                                            }
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
                                            //if (FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == tradeCover).Count() > 0)
                                            //{
                                            //    var tradefeedata = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == tradeCover);
                                            //    PolicyPercentageFee += tradefeedata.Sum(x => x.Attributes.Contains("lux_feepercentage") ? x.GetAttributeValue<decimal>("lux_feepercentage") : 0);

                                            //    foreach (var item1 in FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970001))
                                            //    {
                                            //        if (tradefeedata.Where(x => x.GetAttributeValue<EntityReference>("lux_feetype").Id == item1.GetAttributeValue<EntityReference>("lux_feetype").Id).Count() == 0)
                                            //        {
                                            //            PolicyPercentageFee += item1.Attributes.Contains("lux_feepercentage") ? item1.GetAttributeValue<decimal>("lux_feepercentage") : 0;
                                            //        }
                                            //    }
                                            //}
                                            //else
                                            //{
                                            //    PolicyPercentageFee = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970001).Sum(x => x.Attributes.Contains("lux_feepercentage") ? x.GetAttributeValue<decimal>("lux_feepercentage") : 0);
                                            //}

                                            //if (FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == tradeCover).Count() > 0)
                                            //{
                                            //    var tradefeedata = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == tradeCover);
                                            //    PolicyFlatFee += tradefeedata.Sum(x => x.Attributes.Contains("lux_feepercentage") ? x.GetAttributeValue<decimal>("lux_feepercentage") : 0);

                                            //    foreach (var item1 in FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970001))
                                            //    {
                                            //        if (tradefeedata.Where(x => x.GetAttributeValue<EntityReference>("lux_feetype").Id == item1.GetAttributeValue<EntityReference>("lux_feetype").Id).Count() == 0)
                                            //        {
                                            //            PolicyFlatFee += item1.Attributes.Contains("lux_feepercentage") ? item1.GetAttributeValue<decimal>("lux_feepercentage") : 0;
                                            //        }
                                            //    }
                                            //}
                                            //else
                                            //{
                                            //    PolicyFlatFee = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970001).Sum(x => x.Attributes.Contains("lux_feepercentage") ? x.GetAttributeValue<decimal>("lux_feepercentage") : 0);
                                            //}

                                            PolicyPercentageFee = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_feepercentage") ? x.GetAttributeValue<decimal>("lux_feepercentage") : 0);
                                            PolicyFlatFee = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == tradeCover).Sum(x => x.Attributes.Contains("lux_feeamount") ? x.GetAttributeValue<Money>("lux_feeamount").Value : 0);
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
                                        item["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                        organizationService.Update(item);
                                    }
                                }

                                var quoteOptionFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                      <entity name='lux_phoenixquoteoption'>
                                                        <attribute name='lux_phoenixquoteoptionid' />
                                                        <attribute name='lux_name' />
                                                        <attribute name='createdon' />
                                                        <order attribute='lux_name' descending='false' />
                                                        <filter type='and'>
                                                          <condition attribute='statecode' operator='eq' value='0' />
                                                          <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                        </filter>
                                                      </entity>
                                                    </fetch>";

                                var quoteOptonList = organizationService.RetrieveMultiple(new FetchExpression(quoteOptionFetch));
                                foreach (var item in quoteOptonList.Entities)
                                {
                                    var tradefetch11 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_contractorsplantandequipmentquotepremui'>
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
                                            <attribute name='lux_contractorsplantandequipmentquotepremuiid' />
                                            <order attribute='lux_sectionreference' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <condition attribute='lux_phoenixquoteoption' operator='eq' uiname='' uitype='lux_phoenixquoteoption' value='{item.Id}' />
                                            </filter>
                                          </entity>
                                        </fetch>";

                                    var tradeList11 = organizationService.RetrieveMultiple(new FetchExpression(tradefetch11)).Entities;

                                    Entity totalRow = organizationService.Retrieve("lux_contractorsplantandequipmentquotepremui", tradeList11.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970013).Id, new ColumnSet(false));
                                    totalRow["lux_ratingfigures"] = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_ratingfigures") ? x.GetAttributeValue<Money>("lux_ratingfigures").Value : 0);

                                    var technicalPrem = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_technicalpremium") ? x.GetAttributeValue<Money>("lux_technicalpremium").Value : 0);
                                    var policyPrem = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_policypremium") ? x.GetAttributeValue<Money>("lux_policypremium").Value : 0);
                                    var policyPrembeforeTax = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_policypremiumbeforetax") ? x.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0);

                                    var BrokerCommission = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_brokercommissionamount") ? x.GetAttributeValue<Money>("lux_brokercommissionamount").Value : 0);
                                    var MGACommission = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_mgacommissionamount") ? x.GetAttributeValue<Money>("lux_mgacommissionamount").Value : 0);
                                    var MGUCommission = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_mgucommissionamount") ? x.GetAttributeValue<Money>("lux_mgucommissionamount").Value : 0);
                                    var TaxAmount = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_totaltaxamount") ? x.GetAttributeValue<Money>("lux_totaltaxamount").Value : 0);
                                    var Fee = tradeList11.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_policyfee") ? x.GetAttributeValue<Money>("lux_policyfee").Value : 0);

                                    var PolicyPercentageFeeOverall = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970001).Sum(x => x.Attributes.Contains("lux_feepercentage") ? x.GetAttributeValue<decimal>("lux_feepercentage") : 0);
                                    var PolicyFlatFeeOverall = FeeList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_feebasis").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970001).Sum(x => x.Attributes.Contains("lux_feeamount") ? x.GetAttributeValue<Money>("lux_feeamount").Value : 0);
                                    var PolicyFeeAmtOverall = (policyPrembeforeTax * PolicyPercentageFeeOverall / 100) + PolicyFlatFeeOverall;

                                    if (policyPrembeforeTax != 0)
                                    {
                                        totalRow["lux_technicalpremium"] = new Money(technicalPrem);
                                        totalRow["lux_loaddiscount"] = (policyPrem * 100 / technicalPrem) - 100;
                                        totalRow["lux_justificaiton"] = "NA";
                                        totalRow["lux_brokercommission"] = BrokerCommission * 100 / policyPrembeforeTax;
                                        totalRow["lux_mgacommission"] = MGACommission * 100 / policyPrembeforeTax;
                                        totalRow["lux_mgucommission"] = MGUCommission * 100 / policyPrembeforeTax;
                                        totalRow["lux_totalcommission"] = (MGACommission * 100 / policyPrembeforeTax) + (BrokerCommission * 100 / policyPrembeforeTax);
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
                    //var PremiumQuoteOption = organizationService.Retrieve("lux_phoenixquoteoption", PremiumData.GetAttributeValue<EntityReference>("lux_phoenixquoteoption").Id, new ColumnSet(true));
                    if (SelectedQuoteOptionId != "")
                    {
                        var cpeQuote = organizationService.Retrieve("lux_contractorsplantandequipmentquote", new Guid(cpeQuoteId), new ColumnSet("transactioncurrencyid", "lux_applicationtype"));
                        var ApplicationType = cpeQuote.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value;
                        var selectedQuoteOption = organizationService.Retrieve("lux_phoenixquoteoption", new Guid(SelectedQuoteOptionId), new ColumnSet(true));

                        var tradefetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_contractorsplantandequipmentquotepremui'>
                                            <attribute name='lux_sectionreference' />
                                            <attribute name='lux_technicalpremium' />
                                            <attribute name='lux_mtatechnicalpremium' />
                                            <attribute name='lux_ratingfigures' />
                                            <attribute name='lux_ratedeviation' />
                                            <attribute name='lux_policypremiumbeforetax' />
                                            <attribute name='lux_mtapolicypremiumbeforetax' />
                                            <attribute name='lux_justificaiton' />                                           
                                            <attribute name='lux_brokercommissionamount' />
                                            <attribute name='lux_mtabrokercommissionamount' />
                                            <attribute name='lux_mgacommissionamount' />
                                            <attribute name='lux_mtamgacommissionamount' />
                                            <attribute name='lux_mgucommissionamount' />
                                            <attribute name='lux_mtamgucommissionamount' />
                                            <attribute name='lux_totaltax' />
                                            <attribute name='lux_policyfee' />
                                            <attribute name='lux_mtapolicyfee' />
                                            <attribute name='lux_totaltaxamount' />
                                            <attribute name='lux_mtatotaltaxamount' />
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

                        var TechnicalPremium = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_technicalpremium") ? x.GetAttributeValue<Money>("lux_technicalpremium").Value : 0);
                        var PolicyPremium = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_policypremiumbeforetax") ? x.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0);
                        var BrokerCommission = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_brokercommissionamount") ? x.GetAttributeValue<Money>("lux_brokercommissionamount").Value : 0);
                        var MGACommission = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_mgacommissionamount") ? x.GetAttributeValue<Money>("lux_mgacommissionamount").Value : 0);
                        var MGUCommission = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_mgucommissionamount") ? x.GetAttributeValue<Money>("lux_mgucommissionamount").Value : 0);
                        var TaxAmount = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_totaltaxamount") ? x.GetAttributeValue<Money>("lux_totaltaxamount").Value : 0);
                        var Tax = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_totaltax") ? x.GetAttributeValue<decimal>("lux_totaltax") : 0);
                        var Fee = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970013).Sum(x => x.Attributes.Contains("lux_policyfee") ? x.GetAttributeValue<Money>("lux_policyfee").Value : 0);

                        //throw new InvalidPluginExecutionException(TaxAmount.ToString());
                        if (PolicyPremium != 0)
                        {
                            cpeQuote["lux_technicalpremiumbeforetax"] = new Money(TechnicalPremium);
                            cpeQuote["lux_policypremiumbeforetax"] = new Money(PolicyPremium);
                            cpeQuote["lux_policybrokercommissionamount"] = BrokerCommission;
                            cpeQuote["lux_policybrokercommissionpercentage"] = BrokerCommission * 100 / PolicyPremium;
                            cpeQuote["lux_policymgacommissionpercentage"] = MGACommission * 100 / PolicyPremium;
                            cpeQuote["lux_policyaciesmgucommissionpercentage"] = MGUCommission * 100 / PolicyPremium;
                            cpeQuote["lux_policytotaltaxamount"] = new Money(TaxAmount);
                            cpeQuote["lux_policytotaltax"] = TaxAmount * 100 / PolicyPremium;
                            if (ApplicationType == 972970001)
                            {
                                cpeQuote["lux_policypolicyfee"] = new Money(Fee);
                            }
                        }
                        else
                        {
                            cpeQuote["lux_technicalpremiumbeforetax"] = new Money(0);
                            cpeQuote["lux_policypremiumbeforetax"] = new Money(0);
                            cpeQuote["lux_policybrokercommissionamount"] = BrokerCommission;
                            cpeQuote["lux_policybrokercommissionpercentage"] = null;
                            cpeQuote["lux_policymgacommissionpercentage"] = null;
                            cpeQuote["lux_policyaciesmgucommissionpercentage"] = null;
                            cpeQuote["lux_policytotaltaxamount"] = new Money(TaxAmount);
                            cpeQuote["lux_policytotaltax"] = null;
                            cpeQuote["lux_policypolicyfee"] = new Money(0);
                        }

                        if (ApplicationType == 972970002) //MTA
                        {
                            var MTAPolicyPremium = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_mtapolicypremiumbeforetax") ? x.GetAttributeValue<Money>("lux_mtapolicypremiumbeforetax").Value : 0);
                            var MTATotalTax = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value != 972970013).Sum(x => x.Attributes.Contains("lux_mtatotaltaxamount") ? x.GetAttributeValue<Money>("lux_mtatotaltaxamount").Value : 0);
                            var MTAFee = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970013).Sum(x => x.Attributes.Contains("lux_mtapolicyfee") ? x.GetAttributeValue<Money>("lux_mtapolicyfee").Value : 0);

                            if (PolicyPremium != 0)
                            {
                                cpeQuote["lux_mtapolicypremiumbeforetax"] = new Money(MTAPolicyPremium);
                                cpeQuote["lux_mtatotaltaxamount"] = new Money(MTATotalTax);
                                cpeQuote["lux_mtapolicyfee"] = new Money(MTAFee);
                            }
                            else
                            {
                                cpeQuote["lux_mtapolicypremiumbeforetax"] = new Money(0);
                                cpeQuote["lux_mtatotaltaxamount"] = new Money(0);
                                cpeQuote["lux_mtapolicyfee"] = new Money(0);
                            }
                        }

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
                            item["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            organizationService.Update(item);
                        }

                        var brokerFetch11 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_cpebrokeragent'>
                                                <attribute name='lux_percentageorflatfee' />
                                                <attribute name='lux_percentage' />
                                                <attribute name='lux_commissonamount' />
                                                <attribute name='lux_cpesection' />
                                                <attribute name='lux_companytype' />
                                                <attribute name='lux_company' />
                                                <attribute name='lux_phoenixpolicypremiumbeforetax' />
                                                <attribute name='lux_commissonamountforsubgrid' />
                                                <attribute name='lux_cpebrokeragentid' />
                                                <order attribute='lux_companytype' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                  <condition attribute='lux_cpesection' operator='eq' value='972970001' />
                                                </filter>
                                              </entity>
                                            </fetch>";
                        var brokerList11 = organizationService.RetrieveMultiple(new FetchExpression(brokerFetch11)).Entities;
                        foreach (var item in brokerList11)
                        {
                            var cpeSection = item.GetAttributeValue<OptionSetValue>("lux_cpesection").Value;
                            var PhoenixPolicyPremium = item.Attributes.Contains("lux_phoenixpolicypremiumbeforetax") ? item.GetAttributeValue<Money>("lux_phoenixpolicypremiumbeforetax").Value : 0;
                            if (brokerList.Where(x => x.Attributes["lux_company"].ToString() == item.Attributes["lux_company"].ToString()).Count() > 0)
                            {
                                var SectionalPolicyPremium = brokerList.Where(x => x.Attributes["lux_company"].ToString() == item.Attributes["lux_company"].ToString()).Sum(x => x.Attributes.Contains("lux_sectionalpolicypremiumbeforetax") ? x.GetAttributeValue<Money>("lux_sectionalpolicypremiumbeforetax").Value : 0);
                                item["lux_sectionalpolicypremiumbeforetax"] = new Money(PhoenixPolicyPremium - SectionalPolicyPremium);
                                item["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                organizationService.Update(item);
                            }
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

                        var feeFetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
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
                                                      <condition attribute='lux_section' operator='eq' value='972970001' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                        var feeFetch2 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_phoenixfeetable'>
                                                <attribute name='lux_section' />
                                                <attribute name='lux_feetype' />
                                                <attribute name='lux_feepercentage' />
                                                <attribute name='lux_feebasis' />
                                                <attribute name='lux_capacity' />
                                                <attribute name='lux_feeallocationtype' />
                                                <attribute name='lux_feeamountforsubgrid' />
                                                <attribute name='lux_phoenixfeetableid' />
                                                <order attribute='lux_section' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_feetype' operator='ne' uiname='Policy Admin Fee' uitype='lux_globalfeeprofile' value='4408A265-1899-F011-B41B-00224842BA61' />
                                                  <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                        var feeList = organizationService.RetrieveMultiple(new FetchExpression(feeFetch)).Entities;
                        foreach (var item in feeList)
                        {
                            var cpeSection = item.GetAttributeValue<OptionSetValue>("lux_section").Value;
                            var SectionalPolicyPremium = tradeList1.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == cpeSection - 2).Sum(x => x.Attributes.Contains("lux_policypremiumbeforetax") ? x.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0);
                            item["lux_sectionalpolicypremiumbeforetax"] = new Money(SectionalPolicyPremium);
                            item["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            organizationService.Update(item);
                        }

                        var feeList1 = organizationService.RetrieveMultiple(new FetchExpression(feeFetch1)).Entities;
                        foreach (var item in feeList1)
                        {
                            item["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            organizationService.Update(item);
                        }
                        var TotalLineFee = 0M;
                        var feeList2 = organizationService.RetrieveMultiple(new FetchExpression(feeFetch2)).Entities;
                        foreach (var item in feeList2)
                        {
                            TotalLineFee += item.Attributes.Contains("lux_feeamountforsubgrid") ? item.GetAttributeValue<Money>("lux_feeamountforsubgrid").Value : 0;
                        }
                        cpeQuote["lux_totallinefee"] = new Money(TotalLineFee);

                        if (PolicyPremium == 0)
                        {
                            var TotalBrokerCommission = 0M;
                            var TotalMGACommission = 0M;
                            var TotalMGUCommission = 0M;
                            var TotalTax = 0M;

                            var brokerFetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
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
                                                  <condition attribute='lux_cpesection' operator='eq' value='972970001' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                            var taxFetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_cpequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_section' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_areweraisingthetaxorjustreporting' />
                                                    <attribute name='lux_cpequotetaxtypeid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                      <condition attribute='lux_section' operator='eq' value='972970001' />
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

                            cpeQuote["lux_policybrokercommissionpercentage"] = TotalBrokerCommission;
                            cpeQuote["lux_policymgacommissionpercentage"] = TotalMGACommission;
                            cpeQuote["lux_policyaciesmgucommissionpercentage"] = TotalMGUCommission;
                            cpeQuote["lux_policytotaltax"] = TotalTax;
                        }

                        organizationService.Update(cpeQuote);

                        var cpeQuote1 = organizationService.Retrieve("lux_contractorsplantandequipmentquote", new Guid(cpeQuoteId), new ColumnSet(false));
                        var TotalLinePer = 0M;
                        var TotalLineAmt = 0M;

                        var capacityFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_phoenixcapacitysplittable'>
                                                    <attribute name='lux_linepercentagecalculated' />
                                                    <attribute name='lux_lineamountcalculated' />
                                                    <attribute name='lux_fee' />
                                                    <attribute name='lux_capacity' />
                                                    <attribute name='lux_phoenixcapacitysplittableid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                        foreach (var item in organizationService.RetrieveMultiple(new FetchExpression(capacityFetch)).Entities)
                        {
                            var Fees = 0M;
                            var feeAllocationType1 = feeList2.Where(x => (x.Attributes.Contains("lux_feeallocationtype") ? x.GetAttributeValue<OptionSetValue>("lux_feeallocationtype").Value : 0) == 972970001);
                            var feeAllocationType2 = feeList2.Where(x => (x.Attributes.Contains("lux_feeallocationtype") ? x.GetAttributeValue<OptionSetValue>("lux_feeallocationtype").Value : 0) == 972970002);

                            if (feeAllocationType1.Count() > 0)
                            {
                                var capacity = item.GetAttributeValue<EntityReference>("lux_capacity").Id;
                                var feeRec = feeAllocationType1.Where(x => (x.Attributes.Contains("lux_capacity") ? x.GetAttributeValue<EntityReference>("lux_capacity").Id : new Guid()) == capacity);
                                if (feeRec.Count() > 0)
                                {
                                    Fees += feeRec.Sum(x => x.Attributes.Contains("lux_feeamountforsubgrid") ? x.GetAttributeValue<Money>("lux_feeamountforsubgrid").Value : 0);
                                }
                            }
                            if (feeAllocationType2.Count() > 0)
                            {
                                var percentage = item.Contains("lux_linepercentagecalculated") ? item.GetAttributeValue<decimal>("lux_linepercentagecalculated") : 0;
                                Fees += feeAllocationType2.Sum(x => x.Attributes.Contains("lux_feeamountforsubgrid") ? x.GetAttributeValue<Money>("lux_feeamountforsubgrid").Value : 0) * percentage / 100;
                            }

                            item["lux_fee"] = new Money(Fees);
                            organizationService.Update(item);

                            if (item.Attributes.Contains("lux_lineamountcalculated"))
                            {
                                TotalLineAmt += item.Attributes.Contains("lux_lineamountcalculated") ? item.GetAttributeValue<Money>("lux_lineamountcalculated").Value : 0;
                            }
                            if (item.Attributes.Contains("lux_linepercentagecalculated"))
                            {
                                TotalLinePer += item.Attributes.Contains("lux_linepercentagecalculated") ? item.GetAttributeValue<decimal>("lux_linepercentagecalculated") : 0;
                            }
                        }
                        cpeQuote1["lux_debrisremovalpercentage"] = TotalLinePer;
                        cpeQuote1["lux_totallineamount"] = new Money(TotalLineAmt);

                        organizationService.Update(cpeQuote1);
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
