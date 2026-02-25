using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Plugins
{
    public class VeloCalculatePremium : IPlugin
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

                    var cycleAppln = organizationService.Retrieve("lux_cycleapplicationnew", entity.Id, new ColumnSet(true));
                    //var cycleAppln = new Entity("lux_cycleapplicationnew", entity.Id);
                    var cycleAppln1 = organizationService.Retrieve("lux_cycleapplicationnew", entity.Id, new ColumnSet("lux_basepricevalue", "lux_adminfee", "lux_legalexpensesvalue", "lux_replacementhirevalue", "lux_accessoriesvalue", "lux_worldwidecovervalue", "lux_cyclebreakdownvalue"));

                    bool raceCover = cycleAppln.Attributes.Contains("lux_racecover") ? cycleAppln.GetAttributeValue<bool>("lux_racecover") : false;
                    var DOB = cycleAppln.Attributes.Contains("lux_dateofbirth") ? cycleAppln.GetAttributeValue<DateTime>("lux_dateofbirth") : DateTime.Today;
                    int age = DateTime.Now.Year - DOB.Year;
                    var excess = cycleAppln.Attributes.Contains("lux_excessamount") ? cycleAppln.FormattedValues["lux_excessamount"].ToString().Replace("£", "") : "0";
                    var storageLoc = cycleAppln.Attributes.Contains("lux_storagelocation") ? cycleAppln.GetAttributeValue<OptionSetValue>("lux_storagelocation").Value : 0;
                    var accessories = cycleAppln.Attributes.Contains("lux_additionalaccessories") ? cycleAppln.GetAttributeValue<OptionSetValue>("lux_additionalaccessories").Value : 972970001;
                    var worldWide = cycleAppln.Attributes.Contains("lux_worldwidecover") ? cycleAppln.GetAttributeValue<OptionSetValue>("lux_worldwidecover").Value : 972970001;
                    bool breakdownCover = cycleAppln.Attributes.Contains("lux_cyclebreakdown") ? cycleAppln.GetAttributeValue<bool>("lux_cyclebreakdown") : false;
                    var replacementHire = cycleAppln.Attributes.Contains("lux_replacementhire") ? cycleAppln.GetAttributeValue<OptionSetValue>("lux_replacementhire").Value : 972970001;
                    var legalCover = cycleAppln.Attributes.Contains("lux_legalexpensescover") ? cycleAppln.GetAttributeValue<bool>("lux_legalexpensescover") : false;

                    decimal cycleValue = 0;
                    decimal rateCharged = 0;
                    decimal ageFactor = 0;
                    decimal excessFactor = 0;
                    decimal basePremium = 0;
                    int cycleCount = 0;

                    if (cycleAppln.Attributes.Contains("lux_bicyclevalue") && cycleAppln.GetAttributeValue<Money>("lux_bicyclevalue").Value > 0)
                    {
                        cycleValue = cycleAppln.GetAttributeValue<Money>("lux_bicyclevalue").Value;
                        cycleCount++;
                    }
                    if (cycleAppln.Attributes.Contains("lux_bicyclevalue2") && cycleAppln.GetAttributeValue<Money>("lux_bicyclevalue2").Value > 0)
                    {
                        cycleValue += cycleAppln.GetAttributeValue<Money>("lux_bicyclevalue2").Value;
                        cycleCount++;
                    }
                    if (cycleAppln.Attributes.Contains("lux_bicyclevalue3") && cycleAppln.GetAttributeValue<Money>("lux_bicyclevalue3").Value > 0)
                    {
                        cycleValue += cycleAppln.GetAttributeValue<Money>("lux_bicyclevalue3").Value;
                        cycleCount++;
                    }
                    if (cycleAppln.Attributes.Contains("lux_bicyclevalue4") && cycleAppln.GetAttributeValue<Money>("lux_bicyclevalue4").Value > 0)
                    {
                        cycleValue += cycleAppln.GetAttributeValue<Money>("lux_bicyclevalue4").Value;
                        cycleCount++;
                    }
                    if (cycleAppln.Attributes.Contains("lux_bicyclevalue5") && cycleAppln.GetAttributeValue<Money>("lux_bicyclevalue5").Value > 0)
                    {
                        cycleValue += cycleAppln.GetAttributeValue<Money>("lux_bicyclevalue5").Value;
                        cycleCount++;
                    }
                    if (cycleAppln.Attributes.Contains("lux_bicyclevalue6") && cycleAppln.GetAttributeValue<Money>("lux_bicyclevalue6").Value > 0)
                    {
                        cycleValue += cycleAppln.GetAttributeValue<Money>("lux_bicyclevalue6").Value;
                        cycleCount++;
                    }
                    if (cycleAppln.Attributes.Contains("lux_bicyclevalue7") && cycleAppln.GetAttributeValue<Money>("lux_bicyclevalue7").Value > 0)
                    {
                        cycleValue += cycleAppln.GetAttributeValue<Money>("lux_bicyclevalue7").Value;
                        cycleCount++;
                    }
                    if (cycleAppln.Attributes.Contains("lux_bicyclevalue8") && cycleAppln.GetAttributeValue<Money>("lux_bicyclevalue8").Value > 0)
                    {
                        cycleValue += cycleAppln.GetAttributeValue<Money>("lux_bicyclevalue8").Value;
                        cycleCount++;
                    }
                    if (cycleAppln.Attributes.Contains("lux_bicyclevalue9") && cycleAppln.GetAttributeValue<Money>("lux_bicyclevalue9").Value > 0)
                    {
                        cycleValue += cycleAppln.GetAttributeValue<Money>("lux_bicyclevalue9").Value;
                        cycleCount++;
                    }
                    if (cycleAppln.Attributes.Contains("lux_bicyclevalue10") && cycleAppln.GetAttributeValue<Money>("lux_bicyclevalue10").Value > 0)
                    {
                        cycleValue += cycleAppln.GetAttributeValue<Money>("lux_bicyclevalue10").Value;
                        cycleCount++;
                    }

                    var rateFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_cyclebasepremium'>
                                                <attribute name='createdon' />
                                                <attribute name='lux_ratechargedracecoveryes' />
                                                <attribute name='lux_ratechargedracecoverno' />
                                                <attribute name='lux_cyclevalueu' />
                                                <attribute name='lux_cyclevaluel' />
                                                <attribute name='lux_cyclebasepremiumid' />
                                                <order attribute='createdon' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_cyclevaluel' operator='le' value='{cycleValue}' />
                                                  <condition attribute='lux_cyclevalueu' operator='ge' value='{cycleValue}' />
                                                </filter>
                                              </entity>
                                            </fetch>";
                    var rateList = organizationService.RetrieveMultiple(new FetchExpression(rateFetch));
                    if (rateList.Entities.Count() > 0)
                    {
                        var rate = rateList.Entities.FirstOrDefault();
                        if (raceCover == true)
                            rateCharged = rate.GetAttributeValue<decimal>("lux_ratechargedracecoveryes");
                        else
                            rateCharged = rate.GetAttributeValue<decimal>("lux_ratechargedracecoverno");
                    }
                    else
                    {
                        throw new InvalidPluginExecutionException("Cycle Value Decline");
                    }

                    basePremium = cycleValue * rateCharged;

                    var ageFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_cyclepolicyhoderagefactor'>
                                                <attribute name='createdon' />
                                                <attribute name='lux_factor' />
                                                <attribute name='lux_ageu' />
                                                <attribute name='lux_agel' />
                                                <attribute name='lux_cyclepolicyhoderagefactorid' />
                                                <order attribute='createdon' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_agel' operator='le' value='{age}' />
                                                  <condition attribute='lux_ageu' operator='ge' value='{age}' />
                                                </filter>
                                              </entity>
                                            </fetch>";
                    var ageList = organizationService.RetrieveMultiple(new FetchExpression(ageFetch));
                    if (ageList.Entities.Count() > 0)
                    {
                        var ageData = ageList.Entities.FirstOrDefault();
                        ageFactor = ageData.GetAttributeValue<decimal>("lux_factor");
                    }
                    else
                    {
                        throw new InvalidPluginExecutionException("Policyholder Age Decline");
                    }

                    basePremium = basePremium * ageFactor;

                    if (excess != "0")
                    {
                        var excessFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_cyclevoluntaryexcessfactor'>
                                                <attribute name='createdon' />
                                                <attribute name='lux_factor' />
                                                <attribute name='lux_excess' />
                                                <attribute name='lux_cyclevoluntaryexcessfactorid' />
                                                <order attribute='lux_excess' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_excess' operator='eq' value='{Convert.ToDecimal(excess)}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                        var excessList = organizationService.RetrieveMultiple(new FetchExpression(excessFetch));
                        if (excessList.Entities.Count() > 0)
                        {
                            var excessData = excessList.Entities.FirstOrDefault();
                            excessFactor = excessData.GetAttributeValue<decimal>("lux_factor");
                        }

                        basePremium = basePremium * excessFactor;

                        if (storageLoc == 972970001)
                        {
                            basePremium = basePremium * 0.9M;
                        }
                        else
                        {
                            throw new InvalidPluginExecutionException("Storage Location Decline");
                        }

                        cycleAppln1["lux_basepricevalue"] = new Money(basePremium);

                        if (accessories != 972970001)
                        {
                            var accVal = cycleAppln.FormattedValues["lux_additionalaccessories"].ToString().Replace("£", "");
                            basePremium = basePremium + Convert.ToDecimal(accVal) * 2.5M / 100;
                            cycleAppln1["lux_accessoriesvalue"] = new Money(Convert.ToDecimal(accVal) * 2.5M / 100);
                        }
                        else
                        {
                            cycleAppln1["lux_accessoriesvalue"] = new Money(0);
                        }

                        if (cycleCount == 2)
                        {
                            basePremium = basePremium * 0.9M;
                        }
                        else if (cycleCount >= 3)
                        {
                            basePremium = basePremium * 0.8M;
                        }

                        if (worldWide != 972970001)
                        {
                            var worldValue = cycleAppln.FormattedValues["lux_worldwidecover"].ToString();
                            if (worldValue.Contains("30"))
                            {
                                basePremium = basePremium + 3;
                                cycleAppln1["lux_worldwidecovervalue"] = new Money(3);
                            }
                            else if (worldValue.Contains("60"))
                            {
                                basePremium = basePremium + 9;
                                cycleAppln1["lux_worldwidecovervalue"] = new Money(9);
                            }
                            else if (worldValue.Contains("90"))
                            {
                                basePremium = basePremium + 15.59M;
                                cycleAppln1["lux_worldwidecovervalue"] = new Money(15.59M);
                            }
                            else if (worldValue.Contains("120"))
                            {
                                basePremium = basePremium + 27;
                                cycleAppln1["lux_worldwidecovervalue"] = new Money(27);
                            }
                        }
                        else
                        {
                            cycleAppln1["lux_worldwidecovervalue"] = new Money(0);
                        }

                        if (breakdownCover == true)
                        {
                            basePremium = basePremium + 5;
                            cycleAppln1["lux_cyclebreakdownvalue"] = new Money(5);
                        }
                        else
                        {
                            cycleAppln1["lux_cyclebreakdownvalue"] = new Money(0);
                        }

                        if (replacementHire != 972970001)
                        {
                            var repVal = cycleAppln.FormattedValues["lux_replacementhire"].ToString().Replace("£", "").Replace(",", "");
                            if (repVal.Contains("500"))
                            {
                                basePremium = basePremium + 3;
                                cycleAppln1["lux_replacementhirevalue"] = new Money(3);
                            }
                            else if (repVal.Contains("1000"))
                            {
                                basePremium = basePremium + 6;
                                cycleAppln1["lux_replacementhirevalue"] = new Money(6);
                            }
                        }
                        else
                        {
                            cycleAppln1["lux_replacementhirevalue"] = new Money(0);
                        }

                        cycleAppln1["lux_adminfee"] = new Money(10);

                        if (legalCover == true)
                        {
                            cycleAppln1["lux_legalexpensesvalue"] = new Money(4.46M);
                        }
                        else
                        {
                            cycleAppln1["lux_legalexpensesvalue"] = new Money(0);
                        }

                        organizationService.Update(cycleAppln1);
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