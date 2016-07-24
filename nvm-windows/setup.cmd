@echo OFF

set NVM_LINK_ID=%time::=%
set NVM_LINK_ID=%NVM_LINK_ID: =%

nvm __setup_link__ %NVM_LINK_ID%

set PATH=%PATH%;%APPDATA%\nvm-windows\.links\link-%NVM_LINK_ID%;%APPDATA%\nvm-windows\bin;