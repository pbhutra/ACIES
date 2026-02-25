using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Plugins
{
    public class ACIESIntangicPremiumBkp : IPlugin
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

                    var intangicproduct = organizationService.Retrieve("lux_intangicproduct", entity.Id, new ColumnSet(true));
                    //var intangicproduct1 = organizationService.Retrieve("lux_intangicproduct", entity.Id, new ColumnSet("lux_totalburningrate", "lux_pool_size", "lux_credibility", "lux_totalsuggestedtechnicalrate", "lux_totalselectedtechnicalrate", "lux_totalpricednetlossratio"));
                    var intangicproduct1 = organizationService.Retrieve("lux_intangicproduct", entity.Id, new ColumnSet(false));

                    if (intangicproduct.Attributes.Contains("lux_limit"))
                    {
                        var limit = intangicproduct.GetAttributeValue<Money>("lux_limit").Value;
                        int relativeBenchmark = intangicproduct.GetAttributeValue<OptionSetValue>("lux_relativebenchmark").Value;
                        int undrwritingGroup = intangicproduct.GetAttributeValue<OptionSetValue>("lux_underwritinggroup").Value;
                        Guid frequencyAnaysis = intangicproduct.GetAttributeValue<EntityReference>("lux_frequencyanalysis").Id;
                        Guid cbhSector = intangicproduct.GetAttributeValue<EntityReference>("lux_cbhsector").Id;
                        decimal layer_15val = intangicproduct.GetAttributeValue<decimal>("lux_layer_15");
                        decimal layer_20val = intangicproduct.GetAttributeValue<decimal>("lux_layer_20");
                        decimal layer_25val = intangicproduct.GetAttributeValue<decimal>("lux_layer_25");
                        decimal layer_30val = intangicproduct.GetAttributeValue<decimal>("lux_layer_30");
                        decimal layer_35val = intangicproduct.GetAttributeValue<decimal>("lux_layer_35");

                        decimal meridiancommissionPercent = intangicproduct.Attributes.Contains("lux_meridiancommission") ? intangicproduct.GetAttributeValue<decimal>("lux_meridiancommission") : 0M;
                        decimal intangiccommissionPercent = intangicproduct.Attributes.Contains("lux_intangiccommission") ? intangicproduct.GetAttributeValue<decimal>("lux_intangiccommission") : 0M;
                        decimal skylinecommissionPercent = intangicproduct.Attributes.Contains("lux_skylinecommission") ? intangicproduct.GetAttributeValue<decimal>("lux_skylinecommission") : 0M;
                        decimal brokercommissionPercent = intangicproduct.Attributes.Contains("lux_brokercommission") ? intangicproduct.GetAttributeValue<decimal>("lux_brokercommission") : 0M;
                        decimal totalcommissionPercent = intangicproduct.Attributes.Contains("lux_commission") ? intangicproduct.GetAttributeValue<decimal>("lux_commission") : 0M;
                        decimal policyFee = intangicproduct.Attributes.Contains("lux_policyfee") ? intangicproduct.GetAttributeValue<Money>("lux_policyfee").Value : 0M;

                        //if ((meridiancommissionPercent + intangiccommissionPercent + skylinecommissionPercent + brokercommissionPercent) > 28.5M)
                        //{
                        //    throw new InvalidPluginExecutionException("Total commission should not exceed 28.5%");
                        //}

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

                                intangicproduct1["lux_burningrate_15"] = burning_15 * 100;
                                intangicproduct1["lux_burningrate_20"] = burning_20 * 100;
                                intangicproduct1["lux_burningrate_25"] = burning_25 * 100;
                                intangicproduct1["lux_burningrate_30"] = burning_30 * 100;
                                intangicproduct1["lux_burningrate_35"] = burning_35 * 100;
                                intangicproduct1["lux_totalburningrate"] = TotalBurningRate;

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

                                intangicproduct1["lux_pool_size"] = avgPoolSize;
                                if (avgPoolSize < 25)
                                {
                                    intangicproduct1["lux_credibility"] = new OptionSetValue(972970001);
                                }
                                else
                                {
                                    intangicproduct1["lux_credibility"] = new OptionSetValue(972970002);
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

                                intangicproduct1["lux_technicalsuggestedrate_15"] = technical_15 * 100;
                                intangicproduct1["lux_technicalsuggestedrate_20"] = technical_20 * 100;
                                intangicproduct1["lux_technicalsuggestedrate_25"] = technical_25 * 100;
                                intangicproduct1["lux_technicalsuggestedrate_30"] = technical_30 * 100;
                                intangicproduct1["lux_technicalsuggestedrate_35"] = technical_35 * 100;

                                intangicproduct1["lux_totalsuggestedtechnicalrate"] = TotalTechnicalRate;

                                decimal technicalSelected_15 = intangicproduct.Attributes.Contains("lux_technicalselectedrate_15") ? intangicproduct.GetAttributeValue<decimal>("lux_technicalselectedrate_15") / 100 : technical_15;
                                decimal technicalSelected_20 = intangicproduct.Attributes.Contains("lux_technicalselectedrate_20") ? intangicproduct.GetAttributeValue<decimal>("lux_technicalselectedrate_20") / 100 : technical_20;
                                decimal technicalSelected_25 = intangicproduct.Attributes.Contains("lux_technicalselectedrate_25") ? intangicproduct.GetAttributeValue<decimal>("lux_technicalselectedrate_25") / 100 : technical_25;
                                decimal technicalSelected_30 = intangicproduct.Attributes.Contains("lux_technicalselectedrate_30") ? intangicproduct.GetAttributeValue<decimal>("lux_technicalselectedrate_30") / 100 : technical_30;
                                decimal technicalSelected_35 = intangicproduct.Attributes.Contains("lux_technicalselectedrate_35") ? intangicproduct.GetAttributeValue<decimal>("lux_technicalselectedrate_35") / 100 : technical_35;

                                decimal TotalSelectedTechnicalRate = technicalSelected_15 * layer_15val + technicalSelected_20 * layer_20val + technicalSelected_25 * layer_25val + technicalSelected_30 * layer_30val + technicalSelected_35 * layer_35val;

                                if (!intangicproduct.Attributes.Contains("lux_technicalselectedrate_15"))
                                    intangicproduct1["lux_technicalselectedrate_15"] = technicalSelected_15 * 100;
                                if (!intangicproduct.Attributes.Contains("lux_technicalselectedrate_20"))
                                    intangicproduct1["lux_technicalselectedrate_20"] = technicalSelected_20 * 100;
                                if (!intangicproduct.Attributes.Contains("lux_technicalselectedrate_25"))
                                    intangicproduct1["lux_technicalselectedrate_25"] = technicalSelected_25 * 100;
                                if (!intangicproduct.Attributes.Contains("lux_technicalselectedrate_30"))
                                    intangicproduct1["lux_technicalselectedrate_30"] = technicalSelected_30 * 100;
                                if (!intangicproduct.Attributes.Contains("lux_technicalselectedrate_35"))
                                    intangicproduct1["lux_technicalselectedrate_35"] = technicalSelected_35 * 100;

                                intangicproduct1["lux_totalselectedtechnicalrate"] = TotalSelectedTechnicalRate;

                                if (TotalSelectedTechnicalRate != 0)
                                {
                                    intangicproduct1["lux_pricednetlossratio_15"] = technicalSelected_15 != 0 ? (burning_15 / technicalSelected_15) * 100 : 0;
                                    intangicproduct1["lux_pricednetlossratio_20"] = technicalSelected_20 != 0 ? (burning_20 / technicalSelected_20) * 100 : 0;
                                    intangicproduct1["lux_pricednetlossratio_25"] = technicalSelected_25 != 0 ? (burning_25 / technicalSelected_25) * 100 : 0;
                                    intangicproduct1["lux_pricednetlossratio_30"] = technicalSelected_30 != 0 ? (burning_30 / technicalSelected_30) * 100 : 0;
                                    intangicproduct1["lux_pricednetlossratio_35"] = technicalSelected_35 != 0 ? (burning_35 / technicalSelected_35) * 100 : 0;

                                    intangicproduct1["lux_totalpricednetlossratio"] = (TotalBurningRate / TotalSelectedTechnicalRate) * 100;
                                }

                                var layer_15limit = (Math.Round((limit * layer_15val / 100) / 5000)) * 5000;
                                var layer_20limit = (Math.Round((limit * layer_20val / 100) / 5000)) * 5000;
                                var layer_25limit = (Math.Round((limit * layer_25val / 100) / 5000)) * 5000;
                                var layer_30limit = (Math.Round((limit * layer_30val / 100) / 5000)) * 5000;
                                var layer_35limit = (Math.Round((limit * layer_35val / 100) / 5000)) * 5000;

                                intangicproduct1["lux_layer_15limit"] = new Money(layer_15limit);
                                intangicproduct1["lux_layer_20limit"] = new Money(layer_20limit);
                                intangicproduct1["lux_layer_25limit"] = new Money(layer_25limit);
                                intangicproduct1["lux_layer_30limit"] = new Money(layer_30limit);
                                intangicproduct1["lux_layer_35limit"] = new Money(layer_35limit);

                                var net_15 = Convert.ToInt32(layer_15limit * technicalSelected_15);
                                var net_20 = Convert.ToInt32(layer_20limit * technicalSelected_20);
                                var net_25 = Convert.ToInt32(layer_25limit * technicalSelected_25);
                                var net_30 = Convert.ToInt32(layer_30limit * technicalSelected_30);
                                var net_35 = Convert.ToInt32(layer_35limit * technicalSelected_35);

                                intangicproduct1["lux_netpremium_15"] = new Money(net_15);
                                intangicproduct1["lux_netpremium_20"] = new Money(net_20);
                                intangicproduct1["lux_netpremium_25"] = new Money(net_25);
                                intangicproduct1["lux_netpremium_30"] = new Money(net_30);
                                intangicproduct1["lux_netpremium_35"] = new Money(net_35);

                                if (!intangicproduct.Attributes.Contains("lux_meridiancommission"))
                                {
                                    meridiancommissionPercent = 1M;
                                    intangicproduct1["lux_meridiancommission"] = meridiancommissionPercent;
                                }

                                if (!intangicproduct.Attributes.Contains("lux_skylinecommission"))
                                {
                                    skylinecommissionPercent = 2M;
                                    intangicproduct1["lux_skylinecommission"] = skylinecommissionPercent;
                                }

                                if (!intangicproduct.Attributes.Contains("lux_commission"))
                                {
                                    totalcommissionPercent = 28.5M;
                                    intangicproduct1["lux_commission"] = totalcommissionPercent;
                                }

                                var totalNet = net_15 + net_20 + net_25 + net_30 + net_35;
                                var grossbeforeTax = totalNet / 0.715M;
                                grossbeforeTax = Convert.ToInt32(Math.Round(grossbeforeTax / 5000M) * 5000);

                                var totalcommission = Convert.ToInt32(grossbeforeTax * totalcommissionPercent / 100);

                                intangicproduct1["lux_totalnetpremium"] = new Money(totalNet);
                                intangicproduct1["lux_meridiancommissionamount"] = new Money(Convert.ToInt32(grossbeforeTax * meridiancommissionPercent / 100));
                                intangicproduct1["lux_intangiccommissionamount"] = new Money(Convert.ToInt32(grossbeforeTax * intangiccommissionPercent / 100));
                                intangicproduct1["lux_skylinecommissionamount"] = new Money(Convert.ToInt32(grossbeforeTax * skylinecommissionPercent / 100));
                                intangicproduct1["lux_brokercommissionamount"] = new Money(Convert.ToInt32(grossbeforeTax * brokercommissionPercent / 100));
                                intangicproduct1["lux_commissionamount"] = new Money(totalcommission);
                                intangicproduct1["lux_grosspremiumbeforetax"] = new Money(grossbeforeTax);

                                var taxPerc = intangicproduct.GetAttributeValue<decimal>("lux_localtax");
                                var tax = Convert.ToInt32(grossbeforeTax * taxPerc / 100);
                                var grossafterTax = grossbeforeTax + tax;
                                intangicproduct1["lux_localtaxamount"] = new Money(tax);
                                intangicproduct1["lux_grosspremiumincludingtax"] = new Money(grossafterTax + policyFee);

                                var versionFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_intangicquoteversion'>
                                                            <attribute name='lux_name' />
                                                            <attribute name='createdon' />
                                                            <attribute name='lux_totalnetpremium' />
                                                            <attribute name='lux_commission' />
                                                            <attribute name='lux_policyfee' />
                                                            <attribute name='lux_localtax' />
                                                            <attribute name='lux_layer_25' />
                                                            <attribute name='lux_layer_20' />
                                                            <attribute name='lux_layer_15' />
                                                            <attribute name='lux_grosspremiumincludingtax' />
                                                            <attribute name='lux_grosspremiumbeforetax' />
                                                            <attribute name='lux_intangicquoteversionid' />
                                                            <order attribute='createdon' descending='false' />
                                                          </entity>
                                                        </fetch>";

                                var versionList = organizationService.RetrieveMultiple(new FetchExpression(versionFetch));

                                intangicproduct1["lux_quoteversionscount"] = versionList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("statecode").Value == 0).Count();
                                organizationService.Update(intangicproduct1);

                                if (versionList.Entities.Count() > 0)
                                {
                                    foreach (var item in versionList.Entities)
                                    {
                                        item["lux_relativebenchmark"] = new OptionSetValue(relativeBenchmark);
                                        item["lux_sectorscore_6ma"] = intangicproduct.GetAttributeValue<decimal>("lux_sectorscore_6ma");
                                        item["lux_frequencyanalysis"] = new EntityReference("lux_cutofffrequency", frequencyAnaysis);
                                        item["lux_cbhsector"] = new EntityReference("lux_cbhsector", cbhSector);
                                        item["lux_technicalselectedrate_15"] = null;
                                        item["lux_technicalselectedrate_20"] = null;
                                        item["lux_technicalselectedrate_25"] = null;
                                        item["lux_technicalselectedrate_30"] = null;
                                        item["lux_technicalselectedrate_35"] = null;
                                        organizationService.Update(item);
                                    }
                                }
                            }
                            else
                            {
                                intangicproduct1["lux_burningrate_15"] = 0M;
                                intangicproduct1["lux_burningrate_20"] = 0M;
                                intangicproduct1["lux_burningrate_25"] = 0M;
                                intangicproduct1["lux_burningrate_30"] = 0M;
                                intangicproduct1["lux_burningrate_35"] = 0M;
                                intangicproduct1["lux_totalburningrate"] = 0M;
                                intangicproduct1["lux_pool_size"] = 0M;
                                intangicproduct1["lux_credibility"] = new OptionSetValue(972970001);
                                intangicproduct1["lux_technicalsuggestedrate_15"] = 0M;
                                intangicproduct1["lux_technicalsuggestedrate_20"] = 0M;
                                intangicproduct1["lux_technicalsuggestedrate_25"] = 0M;
                                intangicproduct1["lux_technicalsuggestedrate_30"] = 0M;
                                intangicproduct1["lux_technicalsuggestedrate_35"] = 0M;
                                intangicproduct1["lux_totalsuggestedtechnicalrate"] = 0M;
                                intangicproduct1["lux_technicalselectedrate_15"] = 0M;
                                intangicproduct1["lux_technicalselectedrate_20"] = 0M;
                                intangicproduct1["lux_technicalselectedrate_25"] = 0M;
                                intangicproduct1["lux_technicalselectedrate_30"] = 0M;
                                intangicproduct1["lux_technicalselectedrate_35"] = 0M;
                                intangicproduct1["lux_totalselectedtechnicalrate"] = 0M;
                                intangicproduct1["lux_pricednetlossratio_15"] = 0M;
                                intangicproduct1["lux_pricednetlossratio_20"] = 0M;
                                intangicproduct1["lux_pricednetlossratio_25"] = 0M;
                                intangicproduct1["lux_pricednetlossratio_30"] = 0M;
                                intangicproduct1["lux_pricednetlossratio_35"] = 0M;
                                intangicproduct1["lux_totalpricednetlossratio"] = 0M;

                                intangicproduct1["lux_netpremium_15"] = new Money(0);
                                intangicproduct1["lux_netpremium_20"] = new Money(0);
                                intangicproduct1["lux_netpremium_25"] = new Money(0);
                                intangicproduct1["lux_netpremium_30"] = new Money(0);
                                intangicproduct1["lux_netpremium_35"] = new Money(0);

                                intangicproduct1["lux_totalnetpremium"] = new Money(0);
                                intangicproduct1["lux_meridiancommissionamount"] = new Money(0);
                                intangicproduct1["lux_intangiccommissionamount"] = new Money(0);
                                intangicproduct1["lux_skylinecommissionamount"] = new Money(0);
                                intangicproduct1["lux_brokercommissionamount"] = new Money(0);
                                intangicproduct1["lux_commissionamount"] = new Money(0);
                                intangicproduct1["lux_grosspremiumbeforetax"] = new Money(0);
                                intangicproduct1["lux_localtax"] = Convert.ToDecimal(0);
                                intangicproduct1["lux_localtaxamount"] = new Money(0);
                                intangicproduct1["lux_grosspremiumincludingtax"] = new Money(0);

                                organizationService.Update(intangicproduct1);
                            }
                        }
                        //else
                        //{
                        //    intangicproduct1["lux_burningrate_15"] = 0M;
                        //    intangicproduct1["lux_burningrate_20"] = 0M;
                        //    intangicproduct1["lux_burningrate_25"] = 0M;
                        //    intangicproduct1["lux_burningrate_30"] = 0M;
                        //    intangicproduct1["lux_burningrate_35"] = 0M;
                        //    intangicproduct1["lux_totalburningrate"] = 0M;
                        //    intangicproduct1["lux_pool_size"] = 0M;
                        //    intangicproduct1["lux_credibility"] = new OptionSetValue(972970001);
                        //    intangicproduct1["lux_technicalsuggestedrate_15"] = 0M;
                        //    intangicproduct1["lux_technicalsuggestedrate_20"] = 0M;
                        //    intangicproduct1["lux_technicalsuggestedrate_25"] = 0M;
                        //    intangicproduct1["lux_technicalsuggestedrate_30"] = 0M;
                        //    intangicproduct1["lux_technicalsuggestedrate_35"] = 0M;
                        //    intangicproduct1["lux_totalsuggestedtechnicalrate"] = 0M;
                        //    intangicproduct1["lux_technicalselectedrate_15"] = 0M;
                        //    intangicproduct1["lux_technicalselectedrate_20"] = 0M;
                        //    intangicproduct1["lux_technicalselectedrate_25"] = 0M;
                        //    intangicproduct1["lux_technicalselectedrate_30"] = 0M;
                        //    intangicproduct1["lux_technicalselectedrate_35"] = 0M;
                        //    intangicproduct1["lux_totalselectedtechnicalrate"] = 0M;
                        //    intangicproduct1["lux_pricednetlossratio_15"] = 0M;
                        //    intangicproduct1["lux_pricednetlossratio_20"] = 0M;
                        //    intangicproduct1["lux_pricednetlossratio_25"] = 0M;
                        //    intangicproduct1["lux_pricednetlossratio_30"] = 0M;
                        //    intangicproduct1["lux_pricednetlossratio_35"] = 0M;
                        //    intangicproduct1["lux_totalpricednetlossratio"] = 0M;

                        //    intangicproduct1["lux_netpremium_15"] = new Money(0);
                        //    intangicproduct1["lux_netpremium_20"] = new Money(0);
                        //    intangicproduct1["lux_netpremium_25"] = new Money(0);
                        //    intangicproduct1["lux_netpremium_30"] = new Money(0);
                        //    intangicproduct1["lux_netpremium_35"] = new Money(0);

                        //    intangicproduct1["lux_totalnetpremium"] = new Money(0);
                        //    //intangicproduct1["lux_commission"] = new decimal(0);
                        //    intangicproduct1["lux_commissionamount"] = new Money(0);
                        //    intangicproduct1["lux_grosspremiumbeforetax"] = new Money(0);
                        //    intangicproduct1["lux_localtax"] = Convert.ToDecimal(0);
                        //    intangicproduct1["lux_localtaxamount"] = new Money(0);
                        //    intangicproduct1["lux_grosspremiumincludingtax"] = new Money(0);

                        //    organizationService.Update(intangicproduct1);
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
