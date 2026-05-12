Remove-Service -Name CH170D
New-Service -Name CH170D -BinaryPathName "$($PWD.Path)\CH170D.exe" -StartupType AutomaticDelayedStart
