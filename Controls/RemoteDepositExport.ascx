<%@ Control Language="C#" AutoEventWireup="true" CodeFile="RemoteDepositExport.ascx.cs" Inherits="RockWeb.Plugins.com_shepherdchurch.ImageCashLetter.RemoteDepositExport" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>
        <Rock:NotificationBox ID="nbWarningMessage" runat="server" NotificationBoxType="Danger" Visible="true" />

        <Rock:RockDropDownList ID="ddlFileFormat" runat="server" Label="File Format" Required="true" />

        <Rock:RockTextBox ID="tbBatchId" runat="server" Label="Batch ID" Required="true" />

        <div class="actions margin-t-md">
            <asp:LinkButton ID="lbExport" runat="server" Text="Export" CssClass="btn btn-primary" OnClick="lbExport_Click" />
        </div>

        <asp:Panel ID="pnlSuccess" runat="server" Visible="false" CssClass="alert alert-success margin-t-lg">
            <p>Data has been successfully exported.</p>
            <p>
                <asp:HyperLink ID="hlDownload" runat="server" Text="Download" CssClass="btn btn-success" />
            </p>
        </asp:Panel>
    </ContentTemplate>
</asp:UpdatePanel>
