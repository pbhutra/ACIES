using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections;
using System.Linq;

namespace D365Plugins
{
    public class CreateQuoteOptionsSubscribeML : IPlugin
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

                    if (entity.LogicalName == "lux_subscribemlriskinfo")
                    {
                        var RiskRow = organizationService.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));
                        var subsQuote = organizationService.Retrieve("lux_subscribepiquote", RiskRow.GetAttributeValue<EntityReference>("lux_subscribemlquote").Id, new ColumnSet(true));

                        var quoteoption = RiskRow.Attributes.Contains("lux_wouldyouliketooffermultiplequoteoptions") ? RiskRow.FormattedValues["lux_wouldyouliketooffermultiplequoteoptions"] : "No";

                        var OptionCount = subsQuote.Contains("lux_quoteoptionscount") ? subsQuote.GetAttributeValue<int>("lux_quoteoptionscount") : 0;
                        var targetpremium = subsQuote.Attributes.Contains("lux_targetpremiumexcludingipt") ? subsQuote.GetAttributeValue<Money>("lux_targetpremiumexcludingipt").Value : 0;

                        var QuoteOptionId = new Guid();

                        if (subsQuote.Attributes.Contains("lux_quoteoption1"))
                        {
                            QuoteOptionId = subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption1").Id;
                            if (!subsQuote.Contains("lux_quoteoptions"))
                            {
                                Entity ptAppln = organizationService.Retrieve("lux_subscribepiquote", subsQuote.Id, new ColumnSet(false));
                                ptAppln["lux_quoteoptions"] = new EntityReference("lux_subscribequoteoption", QuoteOptionId);
                                organizationService.Update(ptAppln);
                            }
                        }
                        else
                        {
                            Entity subscribequoteoption = new Entity("lux_subscribequoteoption");
                            subscribequoteoption["lux_name"] = "Quote Option 1";
                            subscribequoteoption["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            subscribequoteoption["lux_subscribeprofessionalindemnityquote"] = new EntityReference("lux_subscribepiquote", subsQuote.Id);
                            subscribequoteoption["lux_technicalaciesmgucommissionpercentage"] = new decimal(2.5);
                            subscribequoteoption["lux_policyaciesmgucommissionpercentage"] = new decimal(2.5);
                            subscribequoteoption["lux_technicalmgacommissionpercentage"] = new decimal(15);
                            subscribequoteoption["lux_policymgacommissionpercentage"] = new decimal(15);
                            subscribequoteoption["lux_targetpremiumexcludingipt"] = new Money(targetpremium);

                            QuoteOptionId = organizationService.Create(subscribequoteoption);

                            Entity ptAppln = organizationService.Retrieve("lux_subscribepiquote", subsQuote.Id, new ColumnSet(false));
                            ptAppln["lux_quoteoption1"] = new EntityReference("lux_subscribequoteoption", QuoteOptionId);
                            ptAppln["lux_quoteoptions"] = new EntityReference("lux_subscribequoteoption", QuoteOptionId);
                            ptAppln["lux_quoteoptionscount"] = 1;
                            organizationService.Update(ptAppln);
                        }

                        var Ratingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                      <entity name='lux_subscribepiquoteoptionlist'>
                                                        <attribute name='lux_name' />
                                                        <attribute name='lux_rownumber' />
                                                        <attribute name='transactioncurrencyid' />
                                                        <attribute name='lux_subscribepiquoteoptionlistid' />
                                                        <order attribute='lux_name' descending='false' />
                                                        <filter type='and'>
                                                          <condition attribute='statecode' operator='eq' value='0' />
                                                          <condition attribute='lux_subscribequoteoption' operator='eq' uiname='' uitype='lux_subscribequoteoption' value='{QuoteOptionId}' />
                                                        </filter>
                                                      </entity>
                                                    </fetch>";

                        var RateItem = organizationService.RetrieveMultiple(new FetchExpression(Ratingfetch)).Entities;

                        var DirectorsandOfficersLiability = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 1);
                        var EntityLiability = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 2);
                        var EmploymentPracticeLiability = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 3);
                        var Crime = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 4);
                        var TrusteeCover = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 5);
                        var StatutoryLiability = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 6);

                        if (RiskRow.Contains("lux_directorsandofficersliabilitylimitofindem"))
                        {
                            var PILimit = 0M;
                            var Excess = RiskRow.Contains("lux_directorsandofficersliabilityexcess") ? RiskRow.GetAttributeValue<decimal>("lux_directorsandofficersliabilityexcess") : 0;

                            var Limit = RiskRow.GetAttributeValue<OptionSetValue>("lux_directorsandofficersliabilitylimitofindem").Value;
                            if (Limit != 972970005)
                            {
                                PILimit = Convert.ToDecimal(RiskRow.FormattedValues["lux_directorsandofficersliabilitylimitofindem"].Replace(",", ""));
                            }
                            else
                            {
                                if (RiskRow.Contains("lux_ifotherlimitpleasespecify"))
                                {
                                    PILimit = RiskRow.GetAttributeValue<decimal>("lux_ifotherlimitpleasespecify");
                                }
                            }

                            Entity directorsandOfficersLiability = new Entity("lux_subscribepiquoteoptionlist");
                            if (DirectorsandOfficersLiability != null)
                            {
                                directorsandOfficersLiability = organizationService.Retrieve("lux_subscribepiquoteoptionlist", DirectorsandOfficersLiability.Id, new ColumnSet(true));
                            }
                            directorsandOfficersLiability["lux_mlcover"] = new OptionSetValue(972970001);
                            directorsandOfficersLiability["lux_mllimitofindemnity"] = new OptionSetValue(Limit);
                            directorsandOfficersLiability["lux_limitofindemnity"] = PILimit;
                            directorsandOfficersLiability["lux_excess"] = Excess;
                            directorsandOfficersLiability["lux_limitofindemnityformatted"] = PILimit.ToString("#,##0.00");
                            directorsandOfficersLiability["lux_excessformatted"] = Excess.ToString("#,##0.00");
                            directorsandOfficersLiability["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            directorsandOfficersLiability["lux_riskcurrency"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                            directorsandOfficersLiability["lux_subscribequoteoption"] = new EntityReference("lux_subscribequoteoption", QuoteOptionId);
                            if (DirectorsandOfficersLiability != null)
                            {
                                organizationService.Update(directorsandOfficersLiability);
                            }
                            else
                            {
                                organizationService.Create(directorsandOfficersLiability);
                            }
                        }

                        if (RiskRow.Contains("lux_isentityliabilitycoverrequired") && RiskRow.GetAttributeValue<bool>("lux_isentityliabilitycoverrequired") == true)
                        {
                            var PILimit = 0M;
                            var Excess = RiskRow.Contains("lux_entityliabilitylimitexcess") ? RiskRow.GetAttributeValue<decimal>("lux_entityliabilitylimitexcess") : 0;

                            var Limit = RiskRow.GetAttributeValue<OptionSetValue>("lux_entityliabilitylimitofindemnity").Value;
                            if (Limit != 972970005)
                            {
                                PILimit = Convert.ToDecimal(RiskRow.FormattedValues["lux_entityliabilitylimitofindemnity"].Replace(",", ""));
                            }
                            else
                            {
                                if (RiskRow.Contains("lux_ifotherlimitpleasespecifyentityliability"))
                                {
                                    PILimit = RiskRow.GetAttributeValue<decimal>("lux_ifotherlimitpleasespecifyentityliability");
                                }
                            }

                            Entity entityLiability = new Entity("lux_subscribepiquoteoptionlist");
                            if (EntityLiability != null)
                            {
                                entityLiability = organizationService.Retrieve("lux_subscribepiquoteoptionlist", EntityLiability.Id, new ColumnSet(true));
                            }
                            entityLiability["lux_mlcover"] = new OptionSetValue(972970002);
                            entityLiability["lux_mllimitofindemnity"] = new OptionSetValue(Limit);
                            entityLiability["lux_limitofindemnity"] = PILimit;
                            entityLiability["lux_excess"] = Excess;
                            entityLiability["lux_limitofindemnityformatted"] = PILimit.ToString("#,##0.00");
                            entityLiability["lux_excessformatted"] = Excess.ToString("#,##0.00");
                            entityLiability["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            entityLiability["lux_riskcurrency"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                            entityLiability["lux_subscribequoteoption"] = new EntityReference("lux_subscribequoteoption", QuoteOptionId);

                            if (EntityLiability != null)
                            {
                                organizationService.Update(entityLiability);
                            }
                            else
                            {
                                organizationService.Create(entityLiability);
                            }
                        }
                        else
                        {
                            foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 2))
                            {
                                organizationService.Delete("lux_subscribepiquoteoptionlist", item.Id);
                            }
                        }

                        if (RiskRow.Contains("lux_iscoverforemployementpracticeliabilityreq") && RiskRow.GetAttributeValue<bool>("lux_iscoverforemployementpracticeliabilityreq") == true)
                        {
                            var PILimit = 0M;
                            var Excess = RiskRow.Contains("lux_employementpracticeliabilityexcess") ? RiskRow.GetAttributeValue<decimal>("lux_employementpracticeliabilityexcess") : 0;

                            var Limit = RiskRow.GetAttributeValue<OptionSetValue>("lux_employmentpracticeliabilitylimitofindemni").Value;
                            if (Limit != 972970005)
                            {
                                PILimit = Convert.ToDecimal(RiskRow.FormattedValues["lux_employmentpracticeliabilitylimitofindemni"].Replace(",", ""));
                            }
                            else
                            {
                                if (RiskRow.Contains("lux_ifotherlimitpleasespecifyemployeepratice"))
                                {
                                    PILimit = RiskRow.GetAttributeValue<decimal>("lux_ifotherlimitpleasespecifyemployeepratice");
                                }
                            }

                            Entity employmentPracticeLiability = new Entity("lux_subscribepiquoteoptionlist");
                            if (EmploymentPracticeLiability != null)
                            {
                                employmentPracticeLiability = organizationService.Retrieve("lux_subscribepiquoteoptionlist", EmploymentPracticeLiability.Id, new ColumnSet(true));
                            }
                            employmentPracticeLiability["lux_mlcover"] = new OptionSetValue(972970003);
                            employmentPracticeLiability["lux_mllimitofindemnity"] = new OptionSetValue(Limit);
                            employmentPracticeLiability["lux_limitofindemnity"] = PILimit;
                            employmentPracticeLiability["lux_excess"] = Excess;
                            employmentPracticeLiability["lux_limitofindemnityformatted"] = PILimit.ToString("#,##0.00");
                            employmentPracticeLiability["lux_excessformatted"] = Excess.ToString("#,##0.00");
                            employmentPracticeLiability["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            employmentPracticeLiability["lux_riskcurrency"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                            employmentPracticeLiability["lux_subscribequoteoption"] = new EntityReference("lux_subscribequoteoption", QuoteOptionId);

                            if (EmploymentPracticeLiability != null)
                            {
                                organizationService.Update(employmentPracticeLiability);
                            }
                            else
                            {
                                organizationService.Create(employmentPracticeLiability);
                            }
                        }
                        else
                        {
                            foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 3))
                            {
                                organizationService.Delete("lux_subscribepiquoteoptionlist", item.Id);
                            }
                        }

                        if (RiskRow.Contains("lux_iscoverforcrimerequired") && RiskRow.GetAttributeValue<bool>("lux_iscoverforcrimerequired") == true)
                        {
                            var PILimit = 0M;
                            var Excess = RiskRow.Contains("lux_crimecoverexcess") ? RiskRow.GetAttributeValue<decimal>("lux_crimecoverexcess") : 0;

                            var Limit = RiskRow.GetAttributeValue<OptionSetValue>("lux_crimelimitofindemnity").Value;
                            if (Limit != 972970004)
                            {
                                PILimit = Convert.ToDecimal(RiskRow.FormattedValues["lux_crimelimitofindemnity"].Replace(",", ""));
                            }
                            else
                            {
                                if (RiskRow.Contains("lux_ifotherlimitpleasespecifycrimecover"))
                                {
                                    PILimit = RiskRow.GetAttributeValue<decimal>("lux_ifotherlimitpleasespecifycrimecover");
                                }
                            }

                            Entity crime = new Entity("lux_subscribepiquoteoptionlist");
                            if (Crime != null)
                            {
                                crime = organizationService.Retrieve("lux_subscribepiquoteoptionlist", Crime.Id, new ColumnSet(true));
                            }

                            crime["lux_mlcover"] = new OptionSetValue(972970004);
                            crime["lux_mllimitofindemnity"] = new OptionSetValue(Limit);
                            crime["lux_limitofindemnity"] = PILimit;
                            crime["lux_excess"] = Excess;
                            crime["lux_limitofindemnityformatted"] = PILimit.ToString("#,##0.00");
                            crime["lux_excessformatted"] = Excess.ToString("#,##0.00");
                            crime["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            crime["lux_riskcurrency"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                            crime["lux_subscribequoteoption"] = new EntityReference("lux_subscribequoteoption", QuoteOptionId);

                            if (Crime != null)
                            {
                                organizationService.Update(crime);
                            }
                            else
                            {
                                organizationService.Create(crime);
                            }
                        }
                        else
                        {
                            foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 4))
                            {
                                organizationService.Delete("lux_subscribepiquoteoptionlist", item.Id);
                            }
                        }

                        if (RiskRow.Contains("lux_istrusteecoverrequired") && RiskRow.GetAttributeValue<bool>("lux_istrusteecoverrequired") == true)
                        {
                            var PILimit = 0M;
                            var Excess = RiskRow.Contains("lux_trusteecoverexcess") ? RiskRow.GetAttributeValue<decimal>("lux_trusteecoverexcess") : 0;

                            var Limit = RiskRow.GetAttributeValue<OptionSetValue>("lux_trusteelimitofindemnity").Value;
                            if (Limit != 972970004)
                            {
                                PILimit = Convert.ToDecimal(RiskRow.FormattedValues["lux_trusteelimitofindemnity"].Replace(",", ""));
                            }
                            else
                            {
                                if (RiskRow.Contains("lux_ifotherlimitpleasespecifytrusteelimit"))
                                {
                                    PILimit = RiskRow.GetAttributeValue<decimal>("lux_ifotherlimitpleasespecifytrusteelimit");
                                }
                            }

                            Entity trusteeCover = new Entity("lux_subscribepiquoteoptionlist");
                            if (TrusteeCover != null)
                            {
                                trusteeCover = organizationService.Retrieve("lux_subscribepiquoteoptionlist", TrusteeCover.Id, new ColumnSet(true));
                            }

                            trusteeCover["lux_mlcover"] = new OptionSetValue(972970005);
                            trusteeCover["lux_mllimitofindemnity"] = new OptionSetValue(Limit);
                            trusteeCover["lux_limitofindemnity"] = PILimit;
                            trusteeCover["lux_excess"] = Excess;
                            trusteeCover["lux_limitofindemnityformatted"] = PILimit.ToString("#,##0.00");
                            trusteeCover["lux_excessformatted"] = Excess.ToString("#,##0.00");
                            trusteeCover["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            trusteeCover["lux_riskcurrency"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                            trusteeCover["lux_subscribequoteoption"] = new EntityReference("lux_subscribequoteoption", QuoteOptionId);

                            if (TrusteeCover != null)
                            {
                                organizationService.Update(trusteeCover);
                            }
                            else
                            {
                                organizationService.Create(trusteeCover);
                            }
                        }
                        else
                        {
                            foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 5))
                            {
                                organizationService.Delete("lux_subscribepiquoteoptionlist", item.Id);
                            }
                        }

                        if (RiskRow.Contains("lux_statutoryliabilitycoverrequired") && RiskRow.GetAttributeValue<bool>("lux_statutoryliabilitycoverrequired") == true)
                        {
                            var PILimit = 0M;
                            var Excess = RiskRow.Contains("lux_statutoryliabilityexcess") ? RiskRow.GetAttributeValue<decimal>("lux_statutoryliabilityexcess") : 0;

                            var Limit = RiskRow.GetAttributeValue<OptionSetValue>("lux_statutoryliabilitylimitofindemnity").Value;
                            if (Limit != 972970004)
                            {
                                PILimit = Convert.ToDecimal(RiskRow.FormattedValues["lux_statutoryliabilitylimitofindemnity"].Replace(",", ""));
                            }
                            else
                            {
                                if (RiskRow.Contains("lux_ifotherlimitpleasespecifystatutoryliabili"))
                                {
                                    PILimit = RiskRow.GetAttributeValue<decimal>("lux_ifotherlimitpleasespecifystatutoryliabili");
                                }
                            }

                            Entity statutoryLiability = new Entity("lux_subscribepiquoteoptionlist");
                            if (StatutoryLiability != null)
                            {
                                statutoryLiability = organizationService.Retrieve("lux_subscribepiquoteoptionlist", StatutoryLiability.Id, new ColumnSet(true));
                            }

                            statutoryLiability["lux_mlcover"] = new OptionSetValue(972970006);
                            statutoryLiability["lux_mllimitofindemnity"] = new OptionSetValue(Limit);
                            statutoryLiability["lux_limitofindemnity"] = PILimit;
                            statutoryLiability["lux_excess"] = Excess;
                            statutoryLiability["lux_limitofindemnityformatted"] = PILimit.ToString("#,##0.00");
                            statutoryLiability["lux_excessformatted"] = Excess.ToString("#,##0.00");
                            statutoryLiability["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            statutoryLiability["lux_riskcurrency"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                            statutoryLiability["lux_subscribequoteoption"] = new EntityReference("lux_subscribequoteoption", QuoteOptionId);

                            if (StatutoryLiability != null)
                            {
                                organizationService.Update(statutoryLiability);
                            }
                            else
                            {
                                organizationService.Create(statutoryLiability);
                            }
                        }
                        else
                        {
                            foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 6))
                            {
                                organizationService.Delete("lux_subscribepiquoteoptionlist", item.Id);
                            }
                        }

                        if (quoteoption == "Yes")
                        {
                            if (OptionCount == 11)
                            {
                                throw new InvalidPluginExecutionException("More than 10 Quote Options can't be added");
                            }

                            if (!subsQuote.Attributes.Contains("lux_quoteoption2") && OptionCount == 2)
                            {
                                Entity subscribequoteoption = new Entity("lux_subscribequoteoption");
                                subscribequoteoption["lux_name"] = "Quote Option 2";
                                subscribequoteoption["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                subscribequoteoption["lux_subscribeprofessionalindemnityquote"] = new EntityReference("lux_subscribepiquote", subsQuote.Id);
                                subscribequoteoption["lux_technicalaciesmgucommissionpercentage"] = new decimal(2.5);
                                subscribequoteoption["lux_policyaciesmgucommissionpercentage"] = new decimal(2.5);
                                subscribequoteoption["lux_technicalmgacommissionpercentage"] = new decimal(15);
                                subscribequoteoption["lux_policymgacommissionpercentage"] = new decimal(15);
                                subscribequoteoption["lux_targetpremiumexcludingipt"] = new Money(targetpremium);
                                var QuoteoptionId = organizationService.Create(subscribequoteoption);

                                Entity ptAppln = organizationService.Retrieve("lux_subscribepiquote", subsQuote.Id, new ColumnSet(false));
                                ptAppln["lux_quoteoption2"] = new EntityReference("lux_subscribequoteoption", QuoteoptionId);
                                organizationService.Update(ptAppln);

                                var taxfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_policytaxamount' />
                                                    <attribute name='lux_subscribequotetaxtypeid' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribequoteoption' operator='eq' uiname='' uitype='lux_subscribequoteoption' value='{subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption1").Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                                var taxItem = organizationService.RetrieveMultiple(new FetchExpression(taxfetch)).Entities;
                                if (taxItem.Count > 0)
                                {
                                    foreach (var TaxRow in taxItem)
                                    {
                                        Entity subscribetax = new Entity("lux_subscribequotetaxtype");
                                        subscribetax["lux_name"] = TaxRow["lux_name"].ToString();
                                        if (TaxRow.Attributes.Contains("lux_taxpercentage"))
                                        {
                                            subscribetax["lux_taxpercentage"] = TaxRow.GetAttributeValue<decimal>("lux_taxpercentage");
                                        }
                                        subscribetax["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                        subscribetax["lux_subscribequoteoption"] = new EntityReference("lux_subscribequoteoption", QuoteoptionId);
                                        subscribetax["lux_subscribeprofessionalindemnityquote"] = new EntityReference("lux_subscribepiquote", subsQuote.Id);
                                        organizationService.Create(subscribetax);
                                    }
                                }
                            }

                            if (!subsQuote.Attributes.Contains("lux_quoteoption3") && OptionCount == 3)
                            {
                                Entity subscribequoteoption = new Entity("lux_subscribequoteoption");
                                subscribequoteoption["lux_name"] = "Quote Option 3";
                                subscribequoteoption["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                subscribequoteoption["lux_subscribeprofessionalindemnityquote"] = new EntityReference("lux_subscribepiquote", subsQuote.Id);
                                subscribequoteoption["lux_technicalaciesmgucommissionpercentage"] = new decimal(2.5);
                                subscribequoteoption["lux_policyaciesmgucommissionpercentage"] = new decimal(2.5);
                                subscribequoteoption["lux_technicalmgacommissionpercentage"] = new decimal(15);
                                subscribequoteoption["lux_policymgacommissionpercentage"] = new decimal(15);
                                subscribequoteoption["lux_targetpremiumexcludingipt"] = new Money(targetpremium);
                                var QuoteoptionId = organizationService.Create(subscribequoteoption);

                                Entity ptAppln = organizationService.Retrieve("lux_subscribepiquote", subsQuote.Id, new ColumnSet(false));
                                ptAppln["lux_quoteoption3"] = new EntityReference("lux_subscribequoteoption", QuoteoptionId);
                                organizationService.Update(ptAppln);

                                var taxfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_policytaxamount' />
                                                    <attribute name='lux_subscribequotetaxtypeid' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribequoteoption' operator='eq' uiname='' uitype='lux_subscribequoteoption' value='{subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption1").Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                                var taxItem = organizationService.RetrieveMultiple(new FetchExpression(taxfetch)).Entities;
                                if (taxItem.Count > 0)
                                {
                                    foreach (var TaxRow in taxItem)
                                    {
                                        Entity subscribetax = new Entity("lux_subscribequotetaxtype");
                                        subscribetax["lux_name"] = TaxRow["lux_name"].ToString();
                                        if (TaxRow.Attributes.Contains("lux_taxpercentage"))
                                        {
                                            subscribetax["lux_taxpercentage"] = TaxRow.GetAttributeValue<decimal>("lux_taxpercentage");
                                        }
                                        subscribetax["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                        subscribetax["lux_subscribequoteoption"] = new EntityReference("lux_subscribequoteoption", QuoteoptionId);
                                        subscribetax["lux_subscribeprofessionalindemnityquote"] = new EntityReference("lux_subscribepiquote", subsQuote.Id);
                                        organizationService.Create(subscribetax);
                                    }
                                }
                            }

                            if (!subsQuote.Attributes.Contains("lux_quoteoption4") && OptionCount == 4)
                            {
                                Entity subscribequoteoption = new Entity("lux_subscribequoteoption");
                                subscribequoteoption["lux_name"] = "Quote Option 4";
                                subscribequoteoption["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                subscribequoteoption["lux_subscribeprofessionalindemnityquote"] = new EntityReference("lux_subscribepiquote", subsQuote.Id);
                                subscribequoteoption["lux_technicalaciesmgucommissionpercentage"] = new decimal(2.5);
                                subscribequoteoption["lux_policyaciesmgucommissionpercentage"] = new decimal(2.5);
                                subscribequoteoption["lux_technicalmgacommissionpercentage"] = new decimal(15);
                                subscribequoteoption["lux_policymgacommissionpercentage"] = new decimal(15);
                                subscribequoteoption["lux_targetpremiumexcludingipt"] = new Money(targetpremium);
                                var QuoteoptionId = organizationService.Create(subscribequoteoption);

                                Entity ptAppln = organizationService.Retrieve("lux_subscribepiquote", subsQuote.Id, new ColumnSet(false));
                                ptAppln["lux_quoteoption4"] = new EntityReference("lux_subscribequoteoption", QuoteoptionId);
                                organizationService.Update(ptAppln);

                                var taxfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_policytaxamount' />
                                                    <attribute name='lux_subscribequotetaxtypeid' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribequoteoption' operator='eq' uiname='' uitype='lux_subscribequoteoption' value='{subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption1").Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                                var taxItem = organizationService.RetrieveMultiple(new FetchExpression(taxfetch)).Entities;
                                if (taxItem.Count > 0)
                                {
                                    foreach (var TaxRow in taxItem)
                                    {
                                        Entity subscribetax = new Entity("lux_subscribequotetaxtype");
                                        subscribetax["lux_name"] = TaxRow["lux_name"].ToString();
                                        if (TaxRow.Attributes.Contains("lux_taxpercentage"))
                                        {
                                            subscribetax["lux_taxpercentage"] = TaxRow.GetAttributeValue<decimal>("lux_taxpercentage");
                                        }
                                        subscribetax["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                        subscribetax["lux_subscribequoteoption"] = new EntityReference("lux_subscribequoteoption", QuoteoptionId);
                                        subscribetax["lux_subscribeprofessionalindemnityquote"] = new EntityReference("lux_subscribepiquote", subsQuote.Id);
                                        organizationService.Create(subscribetax);
                                    }
                                }
                            }

                            if (!subsQuote.Attributes.Contains("lux_quoteoption5") && OptionCount == 5)
                            {
                                Entity subscribequoteoption = new Entity("lux_subscribequoteoption");
                                subscribequoteoption["lux_name"] = "Quote Option 5";
                                subscribequoteoption["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                subscribequoteoption["lux_subscribeprofessionalindemnityquote"] = new EntityReference("lux_subscribepiquote", subsQuote.Id);
                                subscribequoteoption["lux_technicalaciesmgucommissionpercentage"] = new decimal(2.5);
                                subscribequoteoption["lux_policyaciesmgucommissionpercentage"] = new decimal(2.5);
                                subscribequoteoption["lux_technicalmgacommissionpercentage"] = new decimal(15);
                                subscribequoteoption["lux_policymgacommissionpercentage"] = new decimal(15);
                                subscribequoteoption["lux_targetpremiumexcludingipt"] = new Money(targetpremium);
                                var QuoteoptionId = organizationService.Create(subscribequoteoption);

                                Entity ptAppln = organizationService.Retrieve("lux_subscribepiquote", subsQuote.Id, new ColumnSet(false));
                                ptAppln["lux_quoteoption5"] = new EntityReference("lux_subscribequoteoption", QuoteoptionId);
                                organizationService.Update(ptAppln);

                                var taxfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_policytaxamount' />
                                                    <attribute name='lux_subscribequotetaxtypeid' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribequoteoption' operator='eq' uiname='' uitype='lux_subscribequoteoption' value='{subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption1").Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                                var taxItem = organizationService.RetrieveMultiple(new FetchExpression(taxfetch)).Entities;
                                if (taxItem.Count > 0)
                                {
                                    foreach (var TaxRow in taxItem)
                                    {
                                        Entity subscribetax = new Entity("lux_subscribequotetaxtype");
                                        subscribetax["lux_name"] = TaxRow["lux_name"].ToString();
                                        if (TaxRow.Attributes.Contains("lux_taxpercentage"))
                                        {
                                            subscribetax["lux_taxpercentage"] = TaxRow.GetAttributeValue<decimal>("lux_taxpercentage");
                                        }
                                        subscribetax["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                        subscribetax["lux_subscribequoteoption"] = new EntityReference("lux_subscribequoteoption", QuoteoptionId);
                                        subscribetax["lux_subscribeprofessionalindemnityquote"] = new EntityReference("lux_subscribepiquote", subsQuote.Id);
                                        organizationService.Create(subscribetax);
                                    }
                                }
                            }

                            if (!subsQuote.Attributes.Contains("lux_quoteoption6") && OptionCount == 6)
                            {
                                Entity subscribequoteoption = new Entity("lux_subscribequoteoption");
                                subscribequoteoption["lux_name"] = "Quote Option 6";
                                subscribequoteoption["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                subscribequoteoption["lux_subscribeprofessionalindemnityquote"] = new EntityReference("lux_subscribepiquote", subsQuote.Id);
                                subscribequoteoption["lux_technicalaciesmgucommissionpercentage"] = new decimal(2.5);
                                subscribequoteoption["lux_policyaciesmgucommissionpercentage"] = new decimal(2.5);
                                subscribequoteoption["lux_technicalmgacommissionpercentage"] = new decimal(15);
                                subscribequoteoption["lux_policymgacommissionpercentage"] = new decimal(15);
                                subscribequoteoption["lux_targetpremiumexcludingipt"] = new Money(targetpremium);
                                var QuoteoptionId = organizationService.Create(subscribequoteoption);

                                Entity ptAppln = organizationService.Retrieve("lux_subscribepiquote", subsQuote.Id, new ColumnSet(false));
                                ptAppln["lux_quoteoption6"] = new EntityReference("lux_subscribequoteoption", QuoteoptionId);
                                organizationService.Update(ptAppln);

                                var taxfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_policytaxamount' />
                                                    <attribute name='lux_subscribequotetaxtypeid' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribequoteoption' operator='eq' uiname='' uitype='lux_subscribequoteoption' value='{subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption1").Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                                var taxItem = organizationService.RetrieveMultiple(new FetchExpression(taxfetch)).Entities;
                                if (taxItem.Count > 0)
                                {
                                    foreach (var TaxRow in taxItem)
                                    {
                                        Entity subscribetax = new Entity("lux_subscribequotetaxtype");
                                        subscribetax["lux_name"] = TaxRow["lux_name"].ToString();
                                        if (TaxRow.Attributes.Contains("lux_taxpercentage"))
                                        {
                                            subscribetax["lux_taxpercentage"] = TaxRow.GetAttributeValue<decimal>("lux_taxpercentage");
                                        }
                                        subscribetax["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                        subscribetax["lux_subscribequoteoption"] = new EntityReference("lux_subscribequoteoption", QuoteoptionId);
                                        subscribetax["lux_subscribeprofessionalindemnityquote"] = new EntityReference("lux_subscribepiquote", subsQuote.Id);
                                        organizationService.Create(subscribetax);
                                    }
                                }
                            }

                            if (!subsQuote.Attributes.Contains("lux_quoteoption7") && OptionCount == 7)
                            {
                                Entity subscribequoteoption = new Entity("lux_subscribequoteoption");
                                subscribequoteoption["lux_name"] = "Quote Option 7";
                                subscribequoteoption["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                subscribequoteoption["lux_subscribeprofessionalindemnityquote"] = new EntityReference("lux_subscribepiquote", subsQuote.Id);
                                subscribequoteoption["lux_technicalaciesmgucommissionpercentage"] = new decimal(2.5);
                                subscribequoteoption["lux_policyaciesmgucommissionpercentage"] = new decimal(2.5);
                                subscribequoteoption["lux_technicalmgacommissionpercentage"] = new decimal(15);
                                subscribequoteoption["lux_policymgacommissionpercentage"] = new decimal(15);
                                subscribequoteoption["lux_targetpremiumexcludingipt"] = new Money(targetpremium);
                                var QuoteoptionId = organizationService.Create(subscribequoteoption);

                                Entity ptAppln = organizationService.Retrieve("lux_subscribepiquote", subsQuote.Id, new ColumnSet(false));
                                ptAppln["lux_quoteoption7"] = new EntityReference("lux_subscribequoteoption", QuoteoptionId);
                                organizationService.Update(ptAppln);

                                var taxfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_policytaxamount' />
                                                    <attribute name='lux_subscribequotetaxtypeid' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribequoteoption' operator='eq' uiname='' uitype='lux_subscribequoteoption' value='{subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption1").Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                                var taxItem = organizationService.RetrieveMultiple(new FetchExpression(taxfetch)).Entities;
                                if (taxItem.Count > 0)
                                {
                                    foreach (var TaxRow in taxItem)
                                    {
                                        Entity subscribetax = new Entity("lux_subscribequotetaxtype");
                                        subscribetax["lux_name"] = TaxRow["lux_name"].ToString();
                                        if (TaxRow.Attributes.Contains("lux_taxpercentage"))
                                        {
                                            subscribetax["lux_taxpercentage"] = TaxRow.GetAttributeValue<decimal>("lux_taxpercentage");
                                        }
                                        subscribetax["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                        subscribetax["lux_subscribequoteoption"] = new EntityReference("lux_subscribequoteoption", QuoteoptionId);
                                        subscribetax["lux_subscribeprofessionalindemnityquote"] = new EntityReference("lux_subscribepiquote", subsQuote.Id);
                                        organizationService.Create(subscribetax);
                                    }
                                }
                            }

                            if (!subsQuote.Attributes.Contains("lux_quoteoption8") && OptionCount == 8)
                            {
                                Entity subscribequoteoption = new Entity("lux_subscribequoteoption");
                                subscribequoteoption["lux_name"] = "Quote Option 8";
                                subscribequoteoption["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                subscribequoteoption["lux_subscribeprofessionalindemnityquote"] = new EntityReference("lux_subscribepiquote", subsQuote.Id);
                                subscribequoteoption["lux_technicalaciesmgucommissionpercentage"] = new decimal(2.5);
                                subscribequoteoption["lux_policyaciesmgucommissionpercentage"] = new decimal(2.5);
                                subscribequoteoption["lux_technicalmgacommissionpercentage"] = new decimal(15);
                                subscribequoteoption["lux_policymgacommissionpercentage"] = new decimal(15);
                                subscribequoteoption["lux_targetpremiumexcludingipt"] = new Money(targetpremium);
                                var QuoteoptionId = organizationService.Create(subscribequoteoption);

                                Entity ptAppln = organizationService.Retrieve("lux_subscribepiquote", subsQuote.Id, new ColumnSet(false));
                                ptAppln["lux_quoteoption8"] = new EntityReference("lux_subscribequoteoption", QuoteoptionId);
                                organizationService.Update(ptAppln);

                                var taxfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_policytaxamount' />
                                                    <attribute name='lux_subscribequotetaxtypeid' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribequoteoption' operator='eq' uiname='' uitype='lux_subscribequoteoption' value='{subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption1").Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                                var taxItem = organizationService.RetrieveMultiple(new FetchExpression(taxfetch)).Entities;
                                if (taxItem.Count > 0)
                                {
                                    foreach (var TaxRow in taxItem)
                                    {
                                        Entity subscribetax = new Entity("lux_subscribequotetaxtype");
                                        subscribetax["lux_name"] = TaxRow["lux_name"].ToString();
                                        if (TaxRow.Attributes.Contains("lux_taxpercentage"))
                                        {
                                            subscribetax["lux_taxpercentage"] = TaxRow.GetAttributeValue<decimal>("lux_taxpercentage");
                                        }
                                        subscribetax["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                        subscribetax["lux_subscribequoteoption"] = new EntityReference("lux_subscribequoteoption", QuoteoptionId);
                                        subscribetax["lux_subscribeprofessionalindemnityquote"] = new EntityReference("lux_subscribepiquote", subsQuote.Id);
                                        organizationService.Create(subscribetax);
                                    }
                                }
                            }

                            if (!subsQuote.Attributes.Contains("lux_quoteoption9") && OptionCount == 9)
                            {
                                Entity subscribequoteoption = new Entity("lux_subscribequoteoption");
                                subscribequoteoption["lux_name"] = "Quote Option 9";
                                subscribequoteoption["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                subscribequoteoption["lux_subscribeprofessionalindemnityquote"] = new EntityReference("lux_subscribepiquote", subsQuote.Id);
                                subscribequoteoption["lux_technicalaciesmgucommissionpercentage"] = new decimal(2.5);
                                subscribequoteoption["lux_policyaciesmgucommissionpercentage"] = new decimal(2.5);
                                subscribequoteoption["lux_technicalmgacommissionpercentage"] = new decimal(15);
                                subscribequoteoption["lux_policymgacommissionpercentage"] = new decimal(15);
                                subscribequoteoption["lux_targetpremiumexcludingipt"] = new Money(targetpremium);
                                var QuoteoptionId = organizationService.Create(subscribequoteoption);

                                Entity ptAppln = organizationService.Retrieve("lux_subscribepiquote", subsQuote.Id, new ColumnSet(false));
                                ptAppln["lux_quoteoption9"] = new EntityReference("lux_subscribequoteoption", QuoteoptionId);
                                organizationService.Update(ptAppln);

                                var taxfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_policytaxamount' />
                                                    <attribute name='lux_subscribequotetaxtypeid' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribequoteoption' operator='eq' uiname='' uitype='lux_subscribequoteoption' value='{subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption1").Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                                var taxItem = organizationService.RetrieveMultiple(new FetchExpression(taxfetch)).Entities;
                                if (taxItem.Count > 0)
                                {
                                    foreach (var TaxRow in taxItem)
                                    {
                                        Entity subscribetax = new Entity("lux_subscribequotetaxtype");
                                        subscribetax["lux_name"] = TaxRow["lux_name"].ToString();
                                        if (TaxRow.Attributes.Contains("lux_taxpercentage"))
                                        {
                                            subscribetax["lux_taxpercentage"] = TaxRow.GetAttributeValue<decimal>("lux_taxpercentage");
                                        }
                                        subscribetax["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                        subscribetax["lux_subscribequoteoption"] = new EntityReference("lux_subscribequoteoption", QuoteoptionId);
                                        subscribetax["lux_subscribeprofessionalindemnityquote"] = new EntityReference("lux_subscribepiquote", subsQuote.Id);
                                        organizationService.Create(subscribetax);
                                    }
                                }
                            }

                            if (!subsQuote.Attributes.Contains("lux_quoteoption10") && OptionCount == 10)
                            {
                                Entity subscribequoteoption = new Entity("lux_subscribequoteoption");
                                subscribequoteoption["lux_name"] = "Quote Option 10";
                                subscribequoteoption["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                subscribequoteoption["lux_subscribeprofessionalindemnityquote"] = new EntityReference("lux_subscribepiquote", subsQuote.Id);
                                subscribequoteoption["lux_technicalaciesmgucommissionpercentage"] = new decimal(2.5);
                                subscribequoteoption["lux_policyaciesmgucommissionpercentage"] = new decimal(2.5);
                                subscribequoteoption["lux_technicalmgacommissionpercentage"] = new decimal(15);
                                subscribequoteoption["lux_policymgacommissionpercentage"] = new decimal(15);
                                subscribequoteoption["lux_targetpremiumexcludingipt"] = new Money(targetpremium);
                                var QuoteoptionId = organizationService.Create(subscribequoteoption);

                                Entity ptAppln = organizationService.Retrieve("lux_subscribepiquote", subsQuote.Id, new ColumnSet(false));
                                ptAppln["lux_quoteoption10"] = new EntityReference("lux_subscribequoteoption", QuoteoptionId);
                                organizationService.Update(ptAppln);

                                var taxfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_policytaxamount' />
                                                    <attribute name='lux_subscribequotetaxtypeid' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribequoteoption' operator='eq' uiname='' uitype='lux_subscribequoteoption' value='{subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption1").Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                                var taxItem = organizationService.RetrieveMultiple(new FetchExpression(taxfetch)).Entities;
                                if (taxItem.Count > 0)
                                {
                                    foreach (var TaxRow in taxItem)
                                    {
                                        Entity subscribetax = new Entity("lux_subscribequotetaxtype");
                                        subscribetax["lux_name"] = TaxRow["lux_name"].ToString();
                                        if (TaxRow.Attributes.Contains("lux_taxpercentage"))
                                        {
                                            subscribetax["lux_taxpercentage"] = TaxRow.GetAttributeValue<decimal>("lux_taxpercentage");
                                        }
                                        subscribetax["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                        subscribetax["lux_subscribequoteoption"] = new EntityReference("lux_subscribequoteoption", QuoteoptionId);
                                        subscribetax["lux_subscribeprofessionalindemnityquote"] = new EntityReference("lux_subscribepiquote", subsQuote.Id);
                                        organizationService.Create(subscribetax);
                                    }
                                }
                            }
                            //}
                            //else
                            //{
                            //Entity subscribequoteoption = new Entity("lux_subscribequoteoption");
                            //subscribequoteoption["lux_name"] = "Quote Option 1";
                            //subscribequoteoption["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            //subscribequoteoption["lux_subscribeprofessionalindemnityquote"] = new EntityReference("lux_subscribepiquote", subsQuote.Id);
                            //var QuoteoptionId = organizationService.Create(subscribequoteoption);

                            //Entity ptAppln = organizationService.Retrieve("lux_subscribepiquote", subsQuote.Id, new ColumnSet(false));
                            //ptAppln["lux_quoteoptions"] = new EntityReference("lux_subscribequoteoption", QuoteoptionId);
                            //ptAppln["lux_quoteoptionscount"] = 1;
                            //organizationService.Update(ptAppln);

                            //if (subsQuote.Contains("lux_pleaseselecttheprofessionalindemnitythein"))
                            //{
                            //    var PILimit = 0M;
                            //    var Turnover = subsQuote.Contains("lux_turnovertotallast") ? subsQuote.GetAttributeValue<decimal>("lux_turnovertotallast") : 0;
                            //    var Excess = Turnover / 100;

                            //    var Limit = subsQuote.GetAttributeValue<OptionSetValue>("lux_pleaseselecttheprofessionalindemnitythein").Value;
                            //    if (Limit != 972970007)
                            //    {
                            //        PILimit = Convert.ToDecimal(subsQuote.FormattedValues["lux_pleaseselecttheprofessionalindemnitythein"].Replace(",", ""));
                            //    }
                            //    else
                            //    {
                            //        if (subsQuote.Contains("lux_piotherlimit"))
                            //        {
                            //            PILimit = subsQuote.GetAttributeValue<decimal>("lux_piotherlimit");
                            //        }
                            //    }

                            //    Entity subscribepiquoteoptionlist = new Entity("lux_subscribepiquoteoptionlist");
                            //    subscribepiquoteoptionlist["lux_cover"] = new OptionSetValue(972970001);
                            //    subscribepiquoteoptionlist["lux_pilimitofindemnity"] = new OptionSetValue(Limit);
                            //    subscribepiquoteoptionlist["lux_limitofindemnity"] = PILimit;
                            //    subscribepiquoteoptionlist["lux_excess"] = Excess;
                            //    subscribepiquoteoptionlist["lux_limitofindemnityformatted"] = PILimit.ToString("#,##0.00");
                            //    subscribepiquoteoptionlist["lux_excessformatted"] = Excess.ToString("#,##0.00");
                            //    subscribepiquoteoptionlist["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            //    subscribepiquoteoptionlist["lux_riskcurrency"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                            //    subscribepiquoteoptionlist["lux_subscribequoteoption"] = new EntityReference("lux_subscribequoteoption", QuoteoptionId);
                            //    organizationService.Create(subscribepiquoteoptionlist);
                            //}

                            //if (subsQuote.Contains("lux_iscoverforfidelityrequired") && subsQuote.GetAttributeValue<bool>("lux_iscoverforfidelityrequired") == true)
                            //{
                            //    var Limit = subsQuote.Contains("lux_fidelitycoverlimitofindemnity") ? subsQuote.GetAttributeValue<decimal>("lux_fidelitycoverlimitofindemnity") : 50000M;
                            //    var Excess = subsQuote.Contains("lux_fidelitycoverexcess") ? subsQuote.GetAttributeValue<decimal>("lux_fidelitycoverexcess") : 5000M;
                            //    Entity subscribepiquoteoptionlist = new Entity("lux_subscribepiquoteoptionlist");
                            //    subscribepiquoteoptionlist["lux_cover"] = new OptionSetValue(972970002);
                            //    subscribepiquoteoptionlist["lux_limitofindemnity"] = Limit;
                            //    subscribepiquoteoptionlist["lux_excess"] = Excess;
                            //    subscribepiquoteoptionlist["lux_limitofindemnityformatted"] = Limit.ToString("#,##0.00");
                            //    subscribepiquoteoptionlist["lux_excessformatted"] = Excess.ToString("#,##0.00");
                            //    subscribepiquoteoptionlist["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            //    subscribepiquoteoptionlist["lux_riskcurrency"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                            //    subscribepiquoteoptionlist["lux_subscribequoteoption"] = new EntityReference("lux_subscribequoteoption", QuoteoptionId);
                            //    organizationService.Create(subscribepiquoteoptionlist);
                            //}

                            //if (subsQuote.Contains("lux_iscoverforpublicliabilityrequired") && subsQuote.GetAttributeValue<bool>("lux_iscoverforpublicliabilityrequired") == true)
                            //{
                            //    var PLLimit = 0M;
                            //    var Limit = subsQuote.GetAttributeValue<OptionSetValue>("lux_pleaseselectthelimitofliabilitytheinsure").Value;
                            //    if (Limit != 972970007)
                            //    {
                            //        PLLimit = Convert.ToDecimal(subsQuote.FormattedValues["lux_pleaseselectthelimitofliabilitytheinsure"].Replace(",", ""));
                            //    }
                            //    else
                            //    {
                            //        if (subsQuote.Contains("lux_ifotherlimitrequiredpleaseprovidedetails"))
                            //        {
                            //            PLLimit = subsQuote.GetAttributeValue<decimal>("lux_ifotherlimitrequiredpleaseprovidedetails");
                            //        }
                            //    }
                            //    var Excess = subsQuote.Contains("lux_publicliabilityexcess") ? subsQuote.GetAttributeValue<decimal>("lux_publicliabilityexcess") : 1000M;

                            //    Entity subscribepiquoteoptionlist = new Entity("lux_subscribepiquoteoptionlist");
                            //    subscribepiquoteoptionlist["lux_cover"] = new OptionSetValue(972970003);
                            //    subscribepiquoteoptionlist["lux_pilimitofindemnity"] = new OptionSetValue(Limit);
                            //    subscribepiquoteoptionlist["lux_limitofindemnity"] = PLLimit;
                            //    subscribepiquoteoptionlist["lux_excess"] = Excess;
                            //    subscribepiquoteoptionlist["lux_limitofindemnityformatted"] = PLLimit.ToString("#,##0.00");
                            //    subscribepiquoteoptionlist["lux_excessformatted"] = Excess.ToString("#,##0.00");
                            //    subscribepiquoteoptionlist["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            //    subscribepiquoteoptionlist["lux_riskcurrency"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                            //    subscribepiquoteoptionlist["lux_subscribequoteoption"] = new EntityReference("lux_subscribequoteoption", QuoteoptionId);
                            //    organizationService.Create(subscribepiquoteoptionlist);
                            //}

                            //if (subsQuote.Contains("lux_iscoverforemployersliabilityrequired") && subsQuote.GetAttributeValue<bool>("lux_iscoverforemployersliabilityrequired") == true)
                            //{
                            //    var Limit = subsQuote.Contains("lux_employersliabilitylimitofliability") ? subsQuote.GetAttributeValue<decimal>("lux_employersliabilitylimitofliability") : 0M;
                            //    var Excess = 0M;

                            //    Entity subscribepiquoteoptionlist = new Entity("lux_subscribepiquoteoptionlist");
                            //    subscribepiquoteoptionlist["lux_cover"] = new OptionSetValue(972970004);
                            //    subscribepiquoteoptionlist["lux_limitofindemnity"] = Limit;
                            //    subscribepiquoteoptionlist["lux_excess"] = Excess;
                            //    subscribepiquoteoptionlist["lux_limitofindemnityformatted"] = Limit.ToString("#,##0.00");
                            //    subscribepiquoteoptionlist["lux_excessformatted"] = Excess.ToString("#,##0.00");
                            //    subscribepiquoteoptionlist["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            //    subscribepiquoteoptionlist["lux_riskcurrency"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                            //    subscribepiquoteoptionlist["lux_subscribequoteoption"] = new EntityReference("lux_subscribequoteoption", QuoteoptionId);
                            //    organizationService.Create(subscribepiquoteoptionlist);
                            //}
                            //}
                        }
                        else
                        {
                            if (subsQuote.Attributes.Contains("lux_quoteoption1"))
                            {
                                Entity Appln = organizationService.Retrieve("lux_subscribepiquote", subsQuote.Id, new ColumnSet(false));
                                Appln["lux_quoteoptions"] = new EntityReference("lux_subscribequoteoption", QuoteOptionId);
                                Appln["lux_quoteoptionscount"] = 1;
                                organizationService.Update(Appln);
                            }

                            if (subsQuote.Attributes.Contains("lux_quoteoption2"))
                            {
                                organizationService.Delete("lux_subscribequoteoption", subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption2").Id);
                            }
                            if (subsQuote.Attributes.Contains("lux_quoteoption3"))
                            {
                                organizationService.Delete("lux_subscribequoteoption", subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption3").Id);
                            }
                            if (subsQuote.Attributes.Contains("lux_quoteoption4"))
                            {
                                organizationService.Delete("lux_subscribequoteoption", subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption4").Id);
                            }
                            if (subsQuote.Attributes.Contains("lux_quoteoption5"))
                            {
                                organizationService.Delete("lux_subscribequoteoption", subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption5").Id);
                            }
                            if (subsQuote.Attributes.Contains("lux_quoteoption6"))
                            {
                                organizationService.Delete("lux_subscribequoteoption", subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption6").Id);
                            }
                            if (subsQuote.Attributes.Contains("lux_quoteoption7"))
                            {
                                organizationService.Delete("lux_subscribequoteoption", subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption7").Id);
                            }
                            if (subsQuote.Attributes.Contains("lux_quoteoption8"))
                            {
                                organizationService.Delete("lux_subscribequoteoption", subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption8").Id);
                            }
                            if (subsQuote.Attributes.Contains("lux_quoteoption9"))
                            {
                                organizationService.Delete("lux_subscribequoteoption", subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption9").Id);
                            }
                            if (subsQuote.Attributes.Contains("lux_quoteoption10"))
                            {
                                organizationService.Delete("lux_subscribequoteoption", subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption10").Id);
                            }

                            Entity ptAppln = organizationService.Retrieve("lux_subscribepiquote", subsQuote.Id, new ColumnSet(false));
                            ptAppln["lux_quoteoptionscount"] = 1;
                            organizationService.Update(ptAppln);
                        }
                    }

                    if (entity.LogicalName == "lux_subscribepiquoteoptionlist")
                    {
                        var TradeRow = organizationService.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));
                        var subsQuoteOption = organizationService.Retrieve("lux_subscribequoteoption", TradeRow.GetAttributeValue<EntityReference>("lux_subscribequoteoption").Id, new ColumnSet("lux_subscribeprofessionalindemnityquote"));
                        var subsQuote = organizationService.Retrieve("lux_subscribepiquote", subsQuoteOption.GetAttributeValue<EntityReference>("lux_subscribeprofessionalindemnityquote").Id, new ColumnSet(false));

                        var tradeCover = TradeRow.Contains("lux_mlcover") ? TradeRow.GetAttributeValue<OptionSetValue>("lux_mlcover").Value : 0;
                        var tradeExcess = TradeRow.Contains("lux_excess") ? TradeRow.GetAttributeValue<decimal>("lux_excess") : 0M;
                        var tradeLimit = TradeRow.Contains("lux_limitofindemnity") ? TradeRow.GetAttributeValue<decimal>("lux_limitofindemnity") : 0M;

                        var tradefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_subscribepiquoteoptionlist'>
                                                <attribute name='createdon' />
                                                <attribute name='lux_limitofindemnity' />
                                                <attribute name='lux_excess' />
                                                <attribute name='lux_mlcover' />
                                                <attribute name='lux_subscribepiquoteoptionlistid' />
                                                <attribute name='lux_subscribequoteoption' />
                                                <order attribute='createdon' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_subscribepiquoteoptionlistid' operator='ne' uiname='' uitype='lux_subscribepiquoteoptionlist' value='{TradeRow.Id}' />
                                                </filter>
                                                <link-entity name='lux_subscribequoteoption' from='lux_subscribequoteoptionid' to='lux_subscribequoteoption' link-type='inner' alias='ab'>
                                                  <filter type='and'>
                                                    <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subsQuote.Id}' />
                                                  </filter>
                                                </link-entity>
                                              </entity>
                                            </fetch>";

                        var tradefetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_subscribepiquoteoptionlist'>
                                                <attribute name='createdon' />
                                                <attribute name='lux_limitofindemnity' />
                                                <attribute name='lux_excess' />
                                                <attribute name='lux_mlcover' />
                                                <attribute name='lux_subscribepiquoteoptionlistid' />
                                                <attribute name='lux_subscribequoteoption' />
                                                <order attribute='createdon' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_subscribequoteoption' operator='eq' uiname='' uitype='lux_subscribequoteoption' value='{subsQuoteOption.Id}' />
                                                  <condition attribute='lux_subscribepiquoteoptionlistid' operator='ne' uiname='' uitype='lux_subscribepiquoteoptionlist' value='{TradeRow.Id}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                        var tradeList = organizationService.RetrieveMultiple(new FetchExpression(tradefetch));
                        var tradeList1 = organizationService.RetrieveMultiple(new FetchExpression(tradefetch1));

                        foreach (var item in tradeList1.Entities)
                        {
                            if (item.GetAttributeValue<OptionSetValue>("lux_mlcover").Value == tradeCover)
                            {
                                //throw new InvalidPluginExecutionException(item.GetAttributeValue<OptionSetValue>("lux_cover").Value.ToString() + ", " + tradeCover);
                                throw new InvalidPluginExecutionException("You can not add more than 1 cover type of the same drop down per quote option");
                            }
                        }

                        //foreach (var item in tradeList.Entities)
                        //{
                        //    if (item.GetAttributeValue<OptionSetValue>("lux_cover").Value == tradeCover)
                        //    {
                        //        if (item.GetAttributeValue<decimal>("lux_excess") == tradeExcess && item.GetAttributeValue<decimal>("lux_limitofindemnity") == tradeLimit)
                        //        {
                        //            throw new InvalidPluginExecutionException("You can not add cover type of the same Limit and same Excess as " + item.FormattedValues["lux_subscribequoteoption"]);
                        //        }
                        //    }
                        //}

                        var TradeRow1 = organizationService.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(false));
                        TradeRow1["lux_limitofindemnityformatted"] = tradeLimit.ToString("#,##0.00");
                        TradeRow1["lux_excessformatted"] = tradeExcess.ToString("#,##0.00");
                        organizationService.Update(TradeRow1);
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