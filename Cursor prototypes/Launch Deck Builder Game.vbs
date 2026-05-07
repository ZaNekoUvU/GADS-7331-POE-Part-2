Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

folder = fso.GetParentFolderName(WScript.ScriptFullName)
batPath = """" & folder & "\Launch Deck Builder Game.bat" & """"

' 1 = normal window (visible), false = do not wait
shell.Run batPath, 1, false
