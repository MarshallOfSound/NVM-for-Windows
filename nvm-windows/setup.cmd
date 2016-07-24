@echo OFF
set LINK_ID=%time::=%
nvm __setup_link__ %LINK_ID%
set PATH=%PATH%;%APPDATA%\nvm-windows\.links\link-%LINK_ID%;%APPDATA%\nvm-windows\bin;