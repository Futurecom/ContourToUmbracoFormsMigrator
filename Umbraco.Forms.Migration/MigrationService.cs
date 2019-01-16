using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using Umbraco.Core;
using umbraco.DataLayer;
using Umbraco.Forms.Core.Enums;
using Umbraco.Forms.Migration.Data.Storage;


namespace Umbraco.Forms.Migration
{
    public class MigrationService
    {
        private const int DefaultStringValueLength = 255;

        public bool IgnoreRecords { get; set; }
        public bool IgnoreObsoleteProperties { get; set; }

        public MigrationService()
        {

        }
        public void Migrate(string connString)
        {
            var sql = DataLayerHelper.CreateSqlHelper(connString);

            // fix RecordFields where DataType is set to 'String' but data is stored as different type
            FixDataTypes(sql);

            // Migrate PreValue Sources

            var migratedPreValueSourceIds = new HashSet<Guid>();

            using (var mPreValueSourceStorage = new PrevalueSourceStorage(sql))
            using (var preValueSourceStorage = new Forms.Data.Storage.PrevalueSourceStorage())
            {
                foreach (var mPvs in mPreValueSourceStorage.GetAllPrevalueSources())
                {
                    if (mPvs.Type != null) // Skip unsupported pre-value source types
                    {
                        var pvs = new Umbraco.Forms.Core.FieldPreValueSource()
                        {
                            Id = mPvs.Id,
                            Name = mPvs.Name,
                            Type = mPvs.Type,
                            Settings = mPvs.Settings
                        };

                        // Important: We need to use the update method, as the insert method would
                        // assign a new GUID as the ID of the pre-value source and thus
                        // break the migration.
                        //
                        // The update method works just as the insert method, with the only difference being that
                        // no ID is assigned and different events are fired.
                        preValueSourceStorage.UpdatePreValueSource(pvs);
                    }
                }

                // Get IDs of all pre-value sources in destination environment.
                migratedPreValueSourceIds.UnionWith(preValueSourceStorage
                                                            .GetAll()
                                                            .Select(pvs => pvs.Id));
            }

            // Migrate Forms

            if (!IgnoreRecords)
            {
                // Fix the UFRecordDataString Value field length to be compatible with the old data.
                FixDataStringLength(sql);
            }

            using (var fs = new FormStorage(sql))
            {
                foreach (var form in fs.GetAllForms(false))
                {
                    var v4Form = new Umbraco.Forms.Core.Form();

                    v4Form.Id = form.Id;
                    v4Form.Name = form.Name;
                    v4Form.DisableDefaultStylesheet = form.DisableDefaultStylesheet;
                    v4Form.FieldIndicationType = (FormFieldIndication)System.Enum.Parse(typeof(FormFieldIndication), ((int)form.FieldIndicationType).ToString());
                    v4Form.GoToPageOnSubmit = form.GoToPageOnSubmit;
                    v4Form.HideFieldValidation = form.HideFieldValidation;
                    v4Form.Indicator = form.Indicator;
                    v4Form.InvalidErrorMessage = form.InvalidErrorMessage;
                    v4Form.ManualApproval = form.ManualApproval;
                    v4Form.MessageOnSubmit = form.MessageOnSubmit;
                    v4Form.RequiredErrorMessage = form.RequiredErrorMessage;
                    v4Form.ShowValidationSummary = form.ShowValidationSummary;
                    v4Form.StoreRecordsLocally = form.StoreRecordsLocally;
                    v4Form.XPathOnSubmit = form.XPathOnSubmit;

                    foreach(var page in form.Pages)
                    {
                        var v4Page = new Umbraco.Forms.Core.Page();

                        if (!IgnoreObsoleteProperties)
                        {
                            v4Page.Id = page.Id;
                            v4Page.Form = v4Form.Id;
                        }
                        v4Page.Caption = page.Caption;


                        foreach(var fieldset in page.FieldSets)
                        {
                            var v4Fieldset = new Umbraco.Forms.Core.FieldSet();
                            v4Fieldset.Id = fieldset.Id;
                            if (!IgnoreObsoleteProperties)
                            {
                                v4Fieldset.Page = v4Page.Id;
                            }
                            v4Fieldset.Caption = fieldset.Caption;

                            var v4Container = new Umbraco.Forms.Core.FieldsetContainer();
                            v4Container.Width = 12;

                            foreach (var field in fieldset.Fields)
                            {
                                var v4Field = new Umbraco.Forms.Core.Field();
                                v4Field.Id = field.Id;
                                v4Field.Caption = field.Caption;
                                v4Field.ToolTip = field.ToolTip;
                                v4Field.FieldTypeId = field.FieldTypeId;
                                v4Field.InvalidErrorMessage = field.InvalidErrorMessage;
                                v4Field.Mandatory = field.Mandatory;
                                v4Field.RequiredErrorMessage = field.RequiredErrorMessage;
                                v4Field.RegEx = field.RegEx;

                                using (var pvs = new PreValueStorage(sql))
                                {
                                    var prevalues = new List<string>();
                                    foreach (var prevalue in pvs.GetAllPreValues(field).OrderBy(x => x.SortOrder))
                                    {
                                        prevalues.Add(prevalue.Value);
                                    }
                                    v4Field.PreValues = prevalues;
                                }

                                if (field.PreValueSourceId != Guid.Empty
                                    && migratedPreValueSourceIds.Contains(field.PreValueSourceId))
                                {
                                    v4Field.PreValueSourceId = field.PreValueSourceId;
                                }

                                v4Field.Condition = new Core.FieldCondition();
                                if (!IgnoreObsoleteProperties)
                                {
                                    v4Field.Condition.Id = field.Condition.Id;
                                }
                                v4Field.Condition.Enabled = field.Condition.Enabled;
                                v4Field.Condition.ActionType = (Core.FieldConditionActionType)System.Enum.Parse(typeof(Core.FieldConditionActionType), ((int)field.Condition.ActionType).ToString()); ;
                                v4Field.Condition.LogicType = (Core.FieldConditionLogicType)System.Enum.Parse(typeof(Core.FieldConditionLogicType), ((int)field.Condition.LogicType).ToString()); ;

                                var rules = new List<Core.FieldConditionRule>();
                                foreach (var rule in field.Condition.Rules)
                                {
                                    var v4Rule = new Core.FieldConditionRule();

                                    if (!IgnoreObsoleteProperties)
                                    {
                                        v4Rule.Id = rule.Id;
                                    }
                                    v4Rule.Field = rule.Field;
                                    v4Rule.Operator = (Core.FieldConditionRuleOperator)System.Enum.Parse(typeof(Core.FieldConditionRuleOperator), ((int)rule.Operator).ToString()); ;
                                    v4Rule.Value = rule.Value;

                                    rules.Add(v4Rule);
                                }
                                v4Field.Condition.Rules = rules;

                                using (var ss = new SettingsStorage(sql))
                                {
                                    foreach (var setting in  ss.GetSettingsAsList(field.Id))
                                    {
                                        v4Field.Settings.Add(setting.Key,setting.Value);
                                    }
                                }
                                v4Container.Fields.Add(v4Field);
                            }

                            v4Fieldset.Containers.Add(v4Container);

                            v4Page.FieldSets.Add(v4Fieldset);
                        }

                        v4Form.Pages.Add(v4Page);
                    }

                    using (var s = new Forms.Data.Storage.FormStorage())
                    {
                        v4Form = s.InsertForm(v4Form);

                        v4Form.Created = form.Created;

                        // Note: The form update is also required to work around issue CON-1051
                        // (field aliases are not set in Umbraco Forms below version 4.3.0 when inserting a new form).
                        s.UpdateForm(v4Form);
                    }

                    using (var ws = new WorkflowStorage(sql))
                    {
                        var wfs = ws.GetAllWorkFlows(form);

                        foreach (var workflow in wfs)
                        {
                            using (var wsv4 = new Forms.Data.Storage.WorkflowStorage())
                            {
                                var v4Workflow = new Core.Workflow();
                                v4Workflow.Name = workflow.Name;
                                v4Workflow.Id = workflow.Id;
                                v4Workflow.Type = workflow.Type;
                                v4Workflow.ExecutesOn = (Core.Enums.FormState)System.Enum.Parse(typeof(Core.Enums.FormState), ((int)workflow.ExecutesOn).ToString());
                                v4Workflow.Form = v4Form.Id;
                                v4Workflow.Settings = workflow.Settings;
                                wsv4.InsertWorkflow(v4Form,v4Workflow);
                            }
                        }
                    }

                    if (!IgnoreRecords)
                    {
                        // store records
                        using (var rs = new RecordStorage(sql))
                        {
                            var records = rs.GetAllRecords(form);
                            using (var rs4 = new Forms.Data.Storage.RecordStorage())
                            {
                                foreach (var r in records)
                                {
                                    //if (rs4.GetRecordByUniqueId(r.Form) != null)
                                    //{
                                    //    // Don't import it again.
                                    //    continue;
                                    //}

                                    var v4Record = new Core.Record();
                                    v4Record.Form = v4Form.Id;
                                    v4Record.Created = r.Created;
                                    v4Record.Updated = r.Updated;
                                    v4Record.State = (FormState)r.State;
                                    v4Record.CurrentPage = r.currentPage;
                                    v4Record.UmbracoPageId = r.umbracoPageId;
                                    v4Record.IP = r.IP;
                                    v4Record.MemberKey = r.MemberKey;
                                    // field values - added in this second step as all values are otherwise deleted and reinserted which is SLOW
                                    v4Record.RecordFields = new Dictionary<Guid, Core.RecordField>();
                                    foreach (var kvp in r.RecordFields)
                                    {
                                        var rf = kvp.Value;
                                        v4Record.RecordFields.Add(kvp.Key, new Core.RecordField
                                        {
                                            Key = rf.Key,
                                            FieldId = rf.FieldId,
                                            Field = GetFormField(v4Form, rf.FieldId), // field needs to be set correctly, otherwise UFRecordData doesn't get written
                                            DataType = (Core.FieldDataType)rf.DataType,
                                            DataTypeAlias = rf.DataTypeAlias,
                                            Values = rf.Values
                                        });
                                    }
                                    v4Record.RecordData = v4Record.GenerateRecordDataAsJson();

                                    rs4.InsertRecord(v4Record, v4Form);

                                    // reset DateTime fields to original value, InsertRecord sets them to DateTime.Now
                                    v4Record.Created = r.Created;
                                    v4Record.Updated = r.Updated;

                                    // Update the record via the database context as we only want to update two columns
                                    // and the UpdateRecord method of the RecordStorage would delete and re-insert all the record field values.
                                    ApplicationContext.Current.DatabaseContext.Database.Update(v4Record);
                                }
                            }
                        }
                    }
                }
            }


        }

        private Core.Field GetFormField(Core.Form form, Guid fieldId)
        {
            foreach(var p in form.Pages)
            {
                foreach(var fs in p.FieldSets)
                {
                    foreach(var c in fs.Containers)
                    {
                        if(c.Fields.Any(x => x.Id == fieldId))
                            return c.Fields.First(x => x.Id == fieldId);
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Fixes DataType values incorrectly set to 'String' for fields that use a different data type
        /// This method changes data in the original database!
        /// </summary>
        private void FixDataTypes(ISqlHelper sqlHelper)
        {
            // We found in our database that UFRecordFields had the DataType set as 'String' for all field values, regardless of the actual data type.
            string sql = "UPDATE [UFRecordFields] Set DataType = 'Bit' WHERE DataType = 'String' AND [Key] IN ( SELECT [Key] FROM [UFRecordDataBit] )";// AND [Key] NOT IN ( SELECT [Key] FROM [UFRecordDataString] )";
            sqlHelper.ExecuteNonQuery(sql);
            sql = "UPDATE [UFRecordFields] Set DataType = 'DateTime' WHERE DataType = 'String' AND [Key] IN ( SELECT [Key] FROM [UFRecordDataDateTime] )";// AND [Key] NOT IN ( SELECT [Key] FROM [UFRecordDataString] )";
            sqlHelper.ExecuteNonQuery(sql);
            sql = "UPDATE [UFRecordFields] Set DataType = 'Integer' WHERE DataType = 'String' AND [Key] IN ( SELECT [Key] FROM [UFRecordDataInteger] )";// AND [Key] NOT IN ( SELECT [Key] FROM [UFRecordDataString] )";
            sqlHelper.ExecuteNonQuery(sql);
            sql = "UPDATE [UFRecordFields] Set DataType = 'LongString' WHERE DataType = 'String' AND [Key] IN ( SELECT [Key] FROM [UFRecordDataLongString] )";// AND [Key] NOT IN ( SELECT [Key] FROM [UFRecordDataString] )";
            sqlHelper.ExecuteNonQuery(sql);
        }

        /// <summary>
        /// Fixes the size of the Value field in UFRecordDataString so that it's compatible with the corresponding old Courier table if necessary.
        /// </summary>
        private void FixDataStringLength(ISqlHelper sqlHelper)
        {
            // Get the max length of a string in the source UFRecordDataString table.
            int maxLength = sqlHelper.ExecuteScalar<int>("SELECT MAX(LEN([Value])) FROM [UFRecordDataString]");

            if (maxLength > DefaultStringValueLength)
            {
                ApplicationContext.Current.DatabaseContext.Database.Execute(
                    string.Format("ALTER TABLE [UFRecordDataString] ALTER COLUMN[Value] NVARCHAR({0});", maxLength)
                    );
            }
        }
    }
}