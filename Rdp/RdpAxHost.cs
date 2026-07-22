using System.ComponentModel;
using System.Windows.Forms;

namespace uJump.Rdp;

/// <summary>
/// Hosts the Microsoft RDP client ActiveX control (mstscax.dll) inside a WinForms
/// control so it can be embedded in WPF via a WindowsFormsHost.
///
/// We deliberately avoid generated COM interop assemblies (aximp/tlbimp), which
/// require the .NET Framework build toolchain. Instead we subclass AxHost with
/// the control's CLSID and talk to the underlying COM object through late-bound
/// IDispatch (a <c>dynamic</c>), which the RDP control fully supports.
/// </summary>
[DesignerCategory("")]
public class RdpAxHost : AxHost
{
    // CLSID of "MsTscAx.MsTscAx.11" (IMsRdpClient9-era control) from mstscax.dll.
    // Passed to AxHost without braces.
    private const string RdpControlClsid = "A0C63C30-F08D-4AB4-907C-34905D770C7D";

    public RdpAxHost() : base(RdpControlClsid)
    {
    }

    /// <summary>
    /// The underlying RDP COM object. Null until the control's window handle
    /// (and therefore the OCX) has been created; call <see cref="EnsureCreated"/>
    /// first if unsure.
    /// </summary>
    public dynamic? Ocx
    {
        get
        {
            EnsureCreated();
            return GetOcx();
        }
    }

    /// <summary>Force creation of the native control/OCX if it does not exist yet.</summary>
    public void EnsureCreated()
    {
        if (!IsHandleCreated)
            CreateControl();
    }
}
