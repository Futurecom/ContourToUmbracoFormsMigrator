<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="ContourToForms.ascx.cs" Inherits="Umbraco.Forms.Migration.ContourToForms" %>
<h4>Umbraco Forms migration</h4>
<p>This tool will help you migrate Contour forms and workflows from a Contour instance to an Umbraco Forms instance, simply provide a connectionstring and hit the start migrate button.</p>
<asp:TextBox ID="TextBox1" runat="server" Height="112px" TextMode="MultiLine" Width="250px" placeholder="server=SERVER;database=DB;user id=USER;password=PW"></asp:TextBox>
<br />
<br />
<asp:CheckBox ID="ignoreRecords" runat="server" Text="Don't import Form Responses" />
<br />
<br />
<asp:CheckBox ID="ignoreObsoleteProperties" runat="server" Text="Don't migrate obsolete properties (ID and form reference of pages, page reference of fieldsets and IDs of conditions and rules)." />
<br />
<br/>
<asp:Button ID="Button1" runat="server" Text="Start Migration" OnClick="Button1_Click" />
<p><asp:Literal ID="Literal1" runat="server"></asp:Literal>
</p>
