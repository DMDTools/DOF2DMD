Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/blank"
Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/picture?path=MatrixCode&duration=5&animation=Fade&pause=20"
Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/text?text=DOF2           &size=L&color=ffffff&font=Matrix&bordercolor=33ffff&bordersize=0&cleanbg=false&duration=3&animation=Right2Right&pause=2"
Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/text?text=          DMD&size=L&color=ffffff&font=Matrix&bordercolor=33ffff&bordersize=0&cleanbg=false&duration=3&animation=Left2Left&pause=2"
Start-Sleep -Seconds 10
Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/text?text=DOF2DMD&size=XS&color=ffffff&font=Matrix&bordercolor=33ffff&bordersize=0&cleanbg=false&duration=0,1&animation=None"
Start-Sleep -Seconds 0.2
Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/text?text=DOF2DMD&size=S&color=ffffff&font=Matrix&bordercolor=33ffff&bordersize=0&cleanbg=false&duration=0,1&animation=None"
Start-Sleep -Seconds 0.2
Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/text?text=DOF2DMD&size=M&color=ffffff&font=Matrix&bordercolor=33ffff&bordersize=0&cleanbg=false&duration=0,1&animation=None"
Start-Sleep -Seconds 0.2
Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/text?text=DOF2DMD&size=L&color=ffffff&font=Matrix&bordercolor=33ffff&bordersize=0&cleanbg=false&duration=0,1&animation=None"
Start-Sleep -Seconds 0.2
#Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/advanced?text=DOF2DMD&path=&size=XL&color=ffffff&font=Matrix&bordercolor=33ffff&bordersize=0&cleanbg=true&duration=0.2&animationin=None&animationout=None"
#Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/picture?path=MatrixCode&duration=15&animation=Fade"

Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/text?text=DOF2DMD&size=XL&color=ffffff&font=Matrix&bordercolor=33ffff&bordersize=0&cleanbg=false&duration=10&animation=None"
