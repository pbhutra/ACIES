using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Plugins
{
    public class ACIESIntangicVersionPremium : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Obtain the execution context from the service provider.
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // The InputParameters collection contains all the data
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {

                // Obtain the target entity from the input parameters.
                Entity entity = (Entity)context.InputParameters["Target"];
                try
                {
                    // Obtain the organization service reference.
                    IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

                    var intangicVersion = organizationService.Retrieve("lux_intangicquoteversion", entity.Id, new ColumnSet(true));
                    var intangicproduct = organizationService.Retrieve("lux_intangicproduct", intangicVersion.GetAttributeValue<EntityReference>("lux_intangicquote").Id, new ColumnSet("lux_quotenumber", "lux_underwritinggroup", "lux_relativebenchmark", "lux_frequencyanalysis", "lux_cbhsector"));
                    var intangicVersion1 = organizationService.Retrieve("lux_intangicquoteversion", entity.Id, new ColumnSet(false));

                    if (intangicVersion.Attributes.Contains("lux_limit"))
                    {
                        var limit = intangicVersion.GetAttributeValue<Money>("lux_limit").Value;
                        int relativeBenchmark = intangicproduct.GetAttributeValue<OptionSetValue>("lux_relativebenchmark").Value;
                        int undrwritingGroup = intangicproduct.GetAttributeValue<OptionSetValue>("lux_underwritinggroup").Value;
                        Guid frequencyAnaysis = intangicproduct.GetAttributeValue<EntityReference>("lux_frequencyanalysis").Id;
                        Guid cbhSector = intangicproduct.GetAttributeValue<EntityReference>("lux_cbhsector").Id;

                        decimal layer_15val = intangicVersion.GetAttributeValue<decimal>("lux_layer_15");
                        decimal layer_20val = intangicVersion.GetAttributeValue<decimal>("lux_layer_20");
                        decimal layer_25val = intangicVersion.GetAttributeValue<decimal>("lux_layer_25");
                        decimal layer_30val = intangicVersion.GetAttributeValue<decimal>("lux_layer_30");
                        decimal layer_35val = intangicVersion.GetAttributeValue<decimal>("lux_layer_35");

                        decimal meridiancommissionPercent = intangicVersion.Attributes.Contains("lux_meridiancommission") ? intangicVersion.GetAttributeValue<decimal>("lux_meridiancommission") : 0M;
                        decimal intangiccommissionPercent = intangicVersion.Attributes.Contains("lux_intangiccommission") ? intangicVersion.GetAttributeValue<decimal>("lux_intangiccommission") : 0M;
                        decimal skylinecommissionPercent = intangicVersion.Attributes.Contains("lux_skylinecommission") ? intangicVersion.GetAttributeValue<decimal>("lux_skylinecommission") : 0M;
                        decimal brokercommissionPercent = intangicVersion.Attributes.Contains("lux_brokercommission") ? intangicVersion.GetAttributeValue<decimal>("lux_brokercommission") : 0M;
                        decimal totalcommissionPercent = intangicVersion.Attributes.Contains("lux_commission") ? intangicVersion.GetAttributeValue<decimal>("lux_commission") : 0M;
                        decimal policyFee = intangicVersion.Attributes.Contains("lux_policyfee") ? intangicVersion.GetAttributeValue<Money>("lux_policyfee").Value : 0M;

                        if (undrwritingGroup != 972970003)
                        {
                            var rateFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                            <entity name='lux_intangicrate'>
                                                <attribute name='lux_cbhsector' />
                                                <attribute name='lux_underwriting_group' />
                                                <attribute name='lux_relativebenchmark' />
                                                <attribute name='lux_layer_rate' />
                                                <attribute name='lux_layer' />
                                                <attribute name='lux_cutoff_date' />
                                                <attribute name='lux_pool_size' />
                                                <attribute name='lux_credibility' />
                                                <attribute name='lux_benchmark_rate' />
                                                <attribute name='lux_risk_score' />
                                                <attribute name='lux_risk_group' />
                                                <attribute name='lux_technical_rate' />
                                                <attribute name='lux_selected_technical_rate' />
                                                <attribute name='lux_burning_rate' />
                                                <attribute name='lux_intangicrateid' />
                                                <order attribute='lux_cbhsector' descending='false' />
                                                <filter type='and'>
                                                    <condition attribute='statecode' operator='eq' value='0' />
                                                    <condition attribute='lux_relativebenchmark' operator='eq' value='{relativeBenchmark}' />
                                                    <condition attribute='lux_underwriting_group' operator='eq' value='{undrwritingGroup}' />
                                                    <condition attribute='lux_cutoff_date' operator='eq' uiname='' uitype='lux_cutofffrequency' value='{frequencyAnaysis}' />
                                                    <condition attribute='lux_cbhsector' operator='eq' uiname='' uitype='lux_cbhsector' value='{cbhSector}' />
                                                </filter>
                                            </entity>
                                       </fetch>";

                            var rateList = organizationService.RetrieveMultiple(new FetchExpression(rateFetch));
                            if (rateList.Entities.Count() > 0)
                            {
                                var rate = rateList.Entities;
                                var layer_15 = rate.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_layer").Value == 972970001);
                                var layer_20 = rate.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_layer").Value == 972970002);
                                var layer_25 = rate.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_layer").Value == 972970003);
                                var layer_30 = rate.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_layer").Value == 972970004);
                                var layer_35 = rate.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_layer").Value == 972970005);

                                var poolCount = 0;
                                var poolSize = 0M;
                                var avgPoolSize = 0M;

                                var burning_15 = 0M;
                                var burning_20 = 0M;
                                var burning_25 = 0M;
                                var burning_30 = 0M;
                                var burning_35 = 0M;

                                var technical_15 = 0M;
                                var technical_25 = 0M;
                                var technical_30 = 0M;
                                var technical_20 = 0M;
                                var technical_35 = 0M;

                                var technical_15_suggested = 0M;
                                var technical_30_suggested = 0M;
                                var technical_20_suggested = 0M;
                                var technical_35_suggested = 0M;
                                var technical_25_suggested = 0M;

                                burning_15 = layer_15 != null ? layer_15.GetAttributeValue<decimal>("lux_burning_rate") : 0M;
                                burning_20 = layer_20 != null ? layer_20.GetAttributeValue<decimal>("lux_burning_rate") : 0M;
                                burning_25 = layer_25 != null ? layer_25.GetAttributeValue<decimal>("lux_burning_rate") : 0M;
                                burning_30 = layer_30 != null ? layer_30.GetAttributeValue<decimal>("lux_burning_rate") : 0M;
                                burning_35 = layer_35 != null ? layer_35.GetAttributeValue<decimal>("lux_burning_rate") : 0M;

                                decimal TotalBurningRate = burning_15 * layer_15val + burning_20 * layer_20val + burning_25 * layer_25val + burning_30 * layer_30val + burning_35 * layer_35val;

                                intangicVersion1["lux_burningrate_15"] = burning_15 * 100;
                                intangicVersion1["lux_burningrate_20"] = burning_20 * 100;
                                intangicVersion1["lux_burningrate_25"] = burning_25 * 100;
                                intangicVersion1["lux_burningrate_30"] = burning_30 * 100;
                                intangicVersion1["lux_burningrate_35"] = burning_35 * 100;
                                intangicVersion1["lux_totalburningrate"] = TotalBurningRate;

                                if (layer_15 != null)
                                {
                                    poolSize += layer_15.GetAttributeValue<decimal>("lux_pool_size");
                                    poolCount++;
                                }
                                if (layer_20 != null)
                                {
                                    poolSize += layer_20.GetAttributeValue<decimal>("lux_pool_size");
                                    poolCount++;
                                }
                                if (layer_25 != null)
                                {
                                    poolSize += layer_25.GetAttributeValue<decimal>("lux_pool_size");
                                    poolCount++;
                                }
                                if (layer_30 != null)
                                {
                                    poolSize += layer_30.GetAttributeValue<decimal>("lux_pool_size");
                                    poolCount++;
                                }
                                if (layer_35 != null)
                                {
                                    poolSize += layer_35.GetAttributeValue<decimal>("lux_pool_size");
                                    poolCount++;
                                }

                                avgPoolSize = poolSize / poolCount;

                                intangicVersion1["lux_pool_size"] = avgPoolSize;
                                if (avgPoolSize < 25)
                                {
                                    intangicVersion1["lux_credibility"] = new OptionSetValue(972970001);
                                }
                                else
                                {
                                    intangicVersion1["lux_credibility"] = new OptionSetValue(972970002);
                                }

                                technical_15 = layer_15 != null ? layer_15.GetAttributeValue<decimal>("lux_selected_technical_rate") : 0M;
                                technical_20 = layer_20 != null ? layer_20.GetAttributeValue<decimal>("lux_selected_technical_rate") : 0M;
                                technical_25 = layer_25 != null ? layer_25.GetAttributeValue<decimal>("lux_selected_technical_rate") : 0M;
                                technical_30 = layer_30 != null ? layer_30.GetAttributeValue<decimal>("lux_selected_technical_rate") : 0M;
                                technical_35 = layer_35 != null ? layer_35.GetAttributeValue<decimal>("lux_selected_technical_rate") : 0M;

                                technical_15_suggested = layer_15 != null ? layer_15.GetAttributeValue<decimal>("lux_selected_technical_rate") : 0M;
                                technical_20_suggested = layer_20 != null ? layer_20.GetAttributeValue<decimal>("lux_selected_technical_rate") : 0M;
                                technical_25_suggested = layer_25 != null ? layer_25.GetAttributeValue<decimal>("lux_selected_technical_rate") : 0M;
                                technical_30_suggested = layer_30 != null ? layer_30.GetAttributeValue<decimal>("lux_selected_technical_rate") : 0M;
                                technical_35_suggested = layer_35 != null ? layer_35.GetAttributeValue<decimal>("lux_selected_technical_rate") : 0M;

                                decimal TotalTechnicalRate = technical_15_suggested * layer_15val + technical_20_suggested * layer_20val + technical_25_suggested * layer_25val + technical_30_suggested * layer_30val + technical_35_suggested * layer_35val;

                                intangicVersion1["lux_technicalsuggestedrate_15"] = technical_15 * 100;
                                intangicVersion1["lux_technicalsuggestedrate_20"] = technical_20 * 100;
                                intangicVersion1["lux_technicalsuggestedrate_25"] = technical_25 * 100;
                                intangicVersion1["lux_technicalsuggestedrate_30"] = technical_30 * 100;
                                intangicVersion1["lux_technicalsuggestedrate_35"] = technical_35 * 100;

                                intangicVersion1["lux_totalsuggestedtechnicalrate"] = TotalTechnicalRate;

                                decimal technicalSelected_15 = intangicVersion.Attributes.Contains("lux_technicalselectedrate_15") ? intangicVersion.GetAttributeValue<decimal>("lux_technicalselectedrate_15") / 100 : technical_15;
                                decimal technicalSelected_20 = intangicVersion.Attributes.Contains("lux_technicalselectedrate_20") ? intangicVersion.GetAttributeValue<decimal>("lux_technicalselectedrate_20") / 100 : technical_20;
                                decimal technicalSelected_25 = intangicVersion.Attributes.Contains("lux_technicalselectedrate_25") ? intangicVersion.GetAttributeValue<decimal>("lux_technicalselectedrate_25") / 100 : technical_25;
                                decimal technicalSelected_30 = intangicVersion.Attributes.Contains("lux_technicalselectedrate_30") ? intangicVersion.GetAttributeValue<decimal>("lux_technicalselectedrate_30") / 100 : technical_30;
                                decimal technicalSelected_35 = intangicVersion.Attributes.Contains("lux_technicalselectedrate_35") ? intangicVersion.GetAttributeValue<decimal>("lux_technicalselectedrate_35") / 100 : technical_35;

                                decimal TotalSelectedTechnicalRate = technicalSelected_15 * layer_15val + technicalSelected_20 * layer_20val + technicalSelected_25 * layer_25val + technicalSelected_30 * layer_30val + technicalSelected_35 * layer_35val;

                                if (!intangicVersion.Attributes.Contains("lux_technicalselectedrate_15"))
                                    intangicVersion1["lux_technicalselectedrate_15"] = technicalSelected_15 * 100;
                                if (!intangicVersion.Attributes.Contains("lux_technicalselectedrate_20"))
                                    intangicVersion1["lux_technicalselectedrate_20"] = technicalSelected_20 * 100;
                                if (!intangicVersion.Attributes.Contains("lux_technicalselectedrate_25"))
                                    intangicVersion1["lux_technicalselectedrate_25"] = technicalSelected_25 * 100;
                                if (!intangicVersion.Attributes.Contains("lux_technicalselectedrate_30"))
                                    intangicVersion1["lux_technicalselectedrate_30"] = technicalSelected_30 * 100;
                                if (!intangicVersion.Attributes.Contains("lux_technicalselectedrate_35"))
                                    intangicVersion1["lux_technicalselectedrate_35"] = technicalSelected_35 * 100;

                                intangicVersion1["lux_totalselectedtechnicalrate"] = TotalSelectedTechnicalRate;


                                var discount = intangicVersion.Attributes.Contains("lux_discount") ? intangicVersion.GetAttributeValue<decimal>("lux_discount") : 0;
                                decimal technicalSelectedloaded_15 = technicalSelected_15 + technicalSelected_15 * discount / 100;
                                decimal technicalSelectedloaded_20 = technicalSelected_20 + technicalSelected_20 * discount / 100;
                                decimal technicalSelectedloaded_25 = technicalSelected_25 + technicalSelected_25 * discount / 100;
                                decimal technicalSelectedloaded_30 = technicalSelected_30 + technicalSelected_30 * discount / 100;
                                decimal technicalSelectedloaded_35 = technicalSelected_35 + technicalSelected_35 * discount / 100;

                                decimal TotalSelectedTechnicalLoadedRate = TotalSelectedTechnicalRate + TotalSelectedTechnicalRate * discount / 100;

                                intangicVersion1["lux_technicalselectedloadedrate_15"] = technicalSelectedloaded_15 * 100;
                                intangicVersion1["lux_technicalselectedloadedrate_20"] = technicalSelectedloaded_20 * 100;
                                intangicVersion1["lux_technicalselectedloadedrate_25"] = technicalSelectedloaded_25 * 100;
                                intangicVersion1["lux_technicalselectedloadedrate_30"] = technicalSelectedloaded_30 * 100;
                                intangicVersion1["lux_technicalselectedloadedrate_35"] = technicalSelectedloaded_35 * 100;

                                intangicVersion1["lux_totalselectedtechnicalloadedrate"] = TotalSelectedTechnicalLoadedRate;

                                if (TotalSelectedTechnicalLoadedRate != 0)
                                {
                                    intangicVersion1["lux_pricednetlossratio_15"] = technicalSelectedloaded_15 != 0 ? (burning_15 / technicalSelectedloaded_15) * 100 : 0;
                                    intangicVersion1["lux_pricednetlossratio_20"] = technicalSelectedloaded_20 != 0 ? (burning_20 / technicalSelectedloaded_20) * 100 : 0;
                                    intangicVersion1["lux_pricednetlossratio_25"] = technicalSelectedloaded_25 != 0 ? (burning_25 / technicalSelectedloaded_25) * 100 : 0;
                                    intangicVersion1["lux_pricednetlossratio_30"] = technicalSelectedloaded_30 != 0 ? (burning_30 / technicalSelectedloaded_30) * 100 : 0;
                                    intangicVersion1["lux_pricednetlossratio_35"] = technicalSelectedloaded_35 != 0 ? (burning_35 / technicalSelectedloaded_35) * 100 : 0;

                                    intangicVersion1["lux_totalpricednetlossratio"] = (TotalBurningRate / TotalSelectedTechnicalLoadedRate) * 100;
                                }

                                var layer_15limit = (Math.Round((limit * layer_15val / 100) / 5000)) * 5000;
                                var layer_20limit = (Math.Round((limit * layer_20val / 100) / 5000)) * 5000;
                                var layer_25limit = (Math.Round((limit * layer_25val / 100) / 5000)) * 5000;
                                var layer_30limit = (Math.Round((limit * layer_30val / 100) / 5000)) * 5000;
                                var layer_35limit = (Math.Round((limit * layer_35val / 100) / 5000)) * 5000;

                                intangicVersion1["lux_layer_15limit"] = new Money(layer_15limit);
                                intangicVersion1["lux_layer_20limit"] = new Money(layer_20limit);
                                intangicVersion1["lux_layer_25limit"] = new Money(layer_25limit);
                                intangicVersion1["lux_layer_30limit"] = new Money(layer_30limit);
                                intangicVersion1["lux_layer_35limit"] = new Money(layer_35limit);

                                var net_15 = Convert.ToInt32(layer_15limit * technicalSelectedloaded_15);
                                var net_20 = Convert.ToInt32(layer_20limit * technicalSelectedloaded_20);
                                var net_25 = Convert.ToInt32(layer_25limit * technicalSelectedloaded_25);
                                var net_30 = Convert.ToInt32(layer_30limit * technicalSelectedloaded_30);
                                var net_35 = Convert.ToInt32(layer_35limit * technicalSelectedloaded_35);

                                intangicVersion1["lux_netpremium_15"] = new Money(net_15);
                                intangicVersion1["lux_netpremium_20"] = new Money(net_20);
                                intangicVersion1["lux_netpremium_25"] = new Money(net_25);
                                intangicVersion1["lux_netpremium_30"] = new Money(net_30);
                                intangicVersion1["lux_netpremium_35"] = new Money(net_35);

                                if (!intangicVersion.Attributes.Contains("lux_meridiancommission"))
                                {
                                    meridiancommissionPercent = 1M;
                                    intangicVersion1["lux_meridiancommission"] = meridiancommissionPercent;
                                }

                                if (!intangicVersion.Attributes.Contains("lux_skylinecommission"))
                                {
                                    skylinecommissionPercent = 2M;
                                    intangicVersion1["lux_skylinecommission"] = skylinecommissionPercent;
                                }

                                if (!intangicVersion.Attributes.Contains("lux_commission"))
                                {
                                    totalcommissionPercent = 28.5M;
                                    intangicVersion1["lux_commission"] = totalcommissionPercent;
                                }

                                var totalNet = net_15 + net_20 + net_25 + net_30 + net_35;
                                var grossbeforeTax = totalNet / 0.715M;
                                grossbeforeTax = Convert.ToInt32(Math.Round(grossbeforeTax / 5000M) * 5000);

                                var totalcommission = Convert.ToInt32(grossbeforeTax * totalcommissionPercent / 100);

                                intangicVersion1["lux_totalnetpremium"] = new Money(totalNet);
                                intangicVersion1["lux_meridiancommissionamount"] = new Money(Convert.ToInt32(grossbeforeTax * meridiancommissionPercent / 100));
                                intangicVersion1["lux_intangiccommissionamount"] = new Money(Convert.ToInt32(grossbeforeTax * intangiccommissionPercent / 100));
                                intangicVersion1["lux_skylinecommissionamount"] = new Money(Convert.ToInt32(grossbeforeTax * skylinecommissionPercent / 100));
                                intangicVersion1["lux_brokercommissionamount"] = new Money(Convert.ToInt32(grossbeforeTax * brokercommissionPercent / 100));
                                intangicVersion1["lux_commissionamount"] = new Money(totalcommission);
                                intangicVersion1["lux_grosspremiumbeforetax"] = new Money(grossbeforeTax);

                                var taxPerc = intangicVersion.GetAttributeValue<decimal>("lux_localtax");
                                var tax = Convert.ToInt32(grossbeforeTax * taxPerc / 100);
                                var grossafterTax = grossbeforeTax + tax;
                                intangicVersion1["lux_localtaxamount"] = new Money(tax);
                                intangicVersion1["lux_grosspremiumincludingtax"] = new Money(grossafterTax + policyFee);

                                if (!intangicVersion.Attributes.Contains("lux_name"))
                                {
                                    var versionFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_intangicquoteversion'>
                                                            <attribute name='lux_name' />
                                                            <attribute name='createdon' />
                                                            <attribute name='lux_totalnetpremium' />
                                                            <attribute name='lux_commission' />
                                                            <attribute name='lux_policyfee' />
                                                            <attribute name='lux_localtax' />
                                                            <attribute name='statecode' />
                                                            <attribute name='lux_layer_25' />
                                                            <attribute name='lux_layer_20' />
                                                            <attribute name='lux_layer_15' />
                                                            <attribute name='lux_grosspremiumincludingtax' />
                                                            <attribute name='lux_grosspremiumbeforetax' />
                                                            <attribute name='lux_intangicquoteversionid' />
                                                            <order attribute='createdon' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='lux_intangicquote' operator='eq' uiname='' uitype='lux_intangicproduct' value='{intangicproduct.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                    var versionList = organizationService.RetrieveMultiple(new FetchExpression(versionFetch));
                                    if (versionList.Entities.Count() > 0)
                                    {
                                        intangicVersion1["lux_name"] = intangicproduct.Attributes["lux_quotenumber"].ToString() + "V" + versionList.Entities.Count();
                                    }

                                    var intangicproduct1 = organizationService.Retrieve("lux_intangicproduct", intangicproduct.Id, new ColumnSet(false));
                                    intangicproduct1["lux_quoteversionscount"] = versionList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("statecode").Value == 0).Count();
                                    if (versionList.Entities.Count() == 1 && !intangicproduct.Attributes.Contains("lux_mainquoteversion"))
                                    {
                                        intangicproduct1["lux_mainquoteversion"] = new EntityReference("lux_intangicquoteversion", versionList.Entities.FirstOrDefault().Id);
                                    }
                                    organizationService.Update(intangicproduct1);
                                }
                                organizationService.Update(intangicVersion1);
                            }
                            else
                            {
                                intangicVersion1["lux_burningrate_15"] = 0M;
                                intangicVersion1["lux_burningrate_20"] = 0M;
                                intangicVersion1["lux_burningrate_25"] = 0M;
                                intangicVersion1["lux_burningrate_30"] = 0M;
                                intangicVersion1["lux_burningrate_35"] = 0M;
                                intangicVersion1["lux_totalburningrate"] = 0M;
                                intangicVersion1["lux_pool_size"] = 0M;
                                intangicVersion1["lux_credibility"] = new OptionSetValue(972970001);
                                intangicVersion1["lux_technicalsuggestedrate_15"] = 0M;
                                intangicVersion1["lux_technicalsuggestedrate_20"] = 0M;
                                intangicVersion1["lux_technicalsuggestedrate_25"] = 0M;
                                intangicVersion1["lux_technicalsuggestedrate_30"] = 0M;
                                intangicVersion1["lux_technicalsuggestedrate_35"] = 0M;
                                intangicVersion1["lux_totalsuggestedtechnicalrate"] = 0M;
                                intangicVersion1["lux_technicalselectedrate_15"] = 0M;
                                intangicVersion1["lux_technicalselectedrate_20"] = 0M;
                                intangicVersion1["lux_technicalselectedrate_25"] = 0M;
                                intangicVersion1["lux_technicalselectedrate_30"] = 0M;
                                intangicVersion1["lux_technicalselectedrate_35"] = 0M;
                                intangicVersion1["lux_totalselectedtechnicalrate"] = 0M;

                                intangicVersion1["lux_technicalselectedloadedrate_15"] = 0M;
                                intangicVersion1["lux_technicalselectedloadedrate_20"] = 0M;
                                intangicVersion1["lux_technicalselectedloadedrate_25"] = 0M;
                                intangicVersion1["lux_technicalselectedloadedrate_30"] = 0M;
                                intangicVersion1["lux_technicalselectedloadedrate_35"] = 0M;
                                intangicVersion1["lux_totalselectedtechnicalloadedrate"] = 0M;

                                intangicVersion1["lux_pricednetlossratio_15"] = 0M;
                                intangicVersion1["lux_pricednetlossratio_20"] = 0M;
                                intangicVersion1["lux_pricednetlossratio_25"] = 0M;
                                intangicVersion1["lux_pricednetlossratio_30"] = 0M;
                                intangicVersion1["lux_pricednetlossratio_35"] = 0M;
                                intangicVersion1["lux_totalpricednetlossratio"] = 0M;

                                intangicVersion1["lux_netpremium_15"] = new Money(0);
                                intangicVersion1["lux_netpremium_20"] = new Money(0);
                                intangicVersion1["lux_netpremium_25"] = new Money(0);
                                intangicVersion1["lux_netpremium_30"] = new Money(0);
                                intangicVersion1["lux_netpremium_35"] = new Money(0);

                                intangicVersion1["lux_totalnetpremium"] = new Money(0);
                                intangicVersion1["lux_meridiancommissionamount"] = new Money(0);
                                intangicVersion1["lux_intangiccommissionamount"] = new Money(0);
                                intangicVersion1["lux_skylinecommissionamount"] = new Money(0);
                                intangicVersion1["lux_brokercommissionamount"] = new Money(0);
                                intangicVersion1["lux_commissionamount"] = new Money(0);
                                intangicVersion1["lux_grosspremiumbeforetax"] = new Money(0);
                                intangicVersion1["lux_localtax"] = Convert.ToDecimal(0);
                                intangicVersion1["lux_localtaxamount"] = new Money(0);
                                intangicVersion1["lux_grosspremiumincludingtax"] = new Money(0);

                                organizationService.Update(intangicVersion1);
                            }
                        }
                        else
                        {
                            throw new InvalidPluginExecutionException("Underwriting Group for this Quote is referred!!");
                        }
                        //else
                        //{
                        //    intangicVersion1["lux_burningrate_15"] = 0M;
                        //    intangicVersion1["lux_burningrate_20"] = 0M;
                        //    intangicVersion1["lux_burningrate_25"] = 0M;
                        //    intangicVersion1["lux_burningrate_30"] = 0M;
                        //    intangicVersion1["lux_burningrate_35"] = 0M;
                        //    intangicVersion1["lux_totalburningrate"] = 0M;
                        //    intangicVersion1["lux_pool_size"] = 0M;
                        //    intangicVersion1["lux_credibility"] = new OptionSetValue(972970001);
                        //    intangicVersion1["lux_technicalsuggestedrate_15"] = 0M;
                        //    intangicVersion1["lux_technicalsuggestedrate_20"] = 0M;
                        //    intangicVersion1["lux_technicalsuggestedrate_25"] = 0M;
                        //    intangicVersion1["lux_technicalsuggestedrate_30"] = 0M;
                        //    intangicVersion1["lux_technicalsuggestedrate_35"] = 0M;
                        //    intangicVersion1["lux_totalsuggestedtechnicalrate"] = 0M;
                        //    intangicVersion1["lux_technicalselectedrate_15"] = 0M;
                        //    intangicVersion1["lux_technicalselectedrate_20"] = 0M;
                        //    intangicVersion1["lux_technicalselectedrate_25"] = 0M;
                        //    intangicVersion1["lux_technicalselectedrate_30"] = 0M;
                        //    intangicVersion1["lux_technicalselectedrate_35"] = 0M;
                        //    intangicVersion1["lux_totalselectedtechnicalrate"] = 0M;
                        //    intangicVersion1["lux_pricednetlossratio_15"] = 0M;
                        //    intangicVersion1["lux_pricednetlossratio_20"] = 0M;
                        //    intangicVersion1["lux_pricednetlossratio_25"] = 0M;
                        //    intangicVersion1["lux_pricednetlossratio_30"] = 0M;
                        //    intangicVersion1["lux_pricednetlossratio_35"] = 0M;
                        //    intangicVersion1["lux_totalpricednetlossratio"] = 0M;

                        //    intangicVersion1["lux_netpremium_15"] = new Money(0);
                        //    intangicVersion1["lux_netpremium_20"] = new Money(0);
                        //    intangicVersion1["lux_netpremium_25"] = new Money(0);
                        //    intangicVersion1["lux_netpremium_30"] = new Money(0);
                        //    intangicVersion1["lux_netpremium_35"] = new Money(0);

                        //    intangicVersion1["lux_totalnetpremium"] = new Money(0);
                        //    //intangicVersion1["lux_commission"] = new decimal(0);
                        //    intangicVersion1["lux_commissionamount"] = new Money(0);
                        //    intangicVersion1["lux_grosspremiumbeforetax"] = new Money(0);
                        //    intangicVersion1["lux_localtax"] = Convert.ToDecimal(0);
                        //    intangicVersion1["lux_localtaxamount"] = new Money(0);
                        //    intangicVersion1["lux_grosspremiumincludingtax"] = new Money(0);

                        //    organizationService.Update(intangicVersion1);
                        //}
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidPluginExecutionException(ex.Message);
                }
            }
        }
    }
}
