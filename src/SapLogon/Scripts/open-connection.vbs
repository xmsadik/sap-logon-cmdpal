' Opens a SAP Logon entry the way a double-click in SAP Logon does: SAP GUI's own logon screen, with the
' system's default client. Uses SAP GUI Scripting (GuiApplication.OpenConnection).
'
' Usage:  cscript //nologo //B open-connection.vbs "<SAP Logon description>" "<path to saplogon.exe>"
' Exit codes: 0 opened, 2 SAP GUI Scripting not available, 3 OpenConnection failed (SAP GUI already showed why).
Option Explicit

Dim description, saplogon, gui, app, waited
description = WScript.Arguments(0)
saplogon = WScript.Arguments(1)

On Error Resume Next

' The scripting engine lives in the running SAP Logon; start it (minimized) when it is not running.
Set app = Engine()
If Not IsObject(app) Then
    CreateObject("WScript.Shell").Run """" & saplogon & """", 7, False
    waited = 0
    Do While Not IsObject(app) And waited < 30000
        WScript.Sleep 500
        waited = waited + 500
        Set app = Engine()
    Loop
End If

If Not IsObject(app) Then WScript.Quit 2

Err.Clear
app.OpenConnection description, True
If Err.Number <> 0 Then WScript.Quit 3
WScript.Quit 0

Function Engine()
    Dim g
    Err.Clear
    Set g = GetObject("SAPGUI")
    If Err.Number = 0 And IsObject(g) Then
        Set Engine = g.GetScriptingEngine
        If Err.Number = 0 Then Exit Function
    End If
    Err.Clear
    Engine = Empty
End Function
