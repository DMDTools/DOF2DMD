Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/picture?path=MatrixCode&duration=15&animation=Fade"
Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/advanced?text=DOF2           &path=&size=L&color=ffffff&font=Matrix&bordercolor=33ffff&bordersize=0&cleanbg=false&duration=15&animationin=ScrollOnRight&animationout=ScrollOffLeft"
Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/advanced?text=          DMD&path=&size=L&color=ffffff&font=Matrix&bordercolor=33ffff&bordersize=0&cleanbg=false&duration=15&animationin=ScrollOnLeft&animationout=ScrollOffRight"
Start-Sleep -Seconds 16
Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/blank"
Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/advanced?text=DOF2DMD&path=&size=XS&color=ffffff&font=Matrix&bordercolor=33ffff&bordersize=0&cleanbg=true&duration=0.2&animationin=None&animationout=None"
Start-Sleep -Seconds 0.2
Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/advanced?text=DOF2DMD&path=&size=S&color=ffffff&font=Matrix&bordercolor=33ffff&bordersize=0&cleanbg=true&duration=0.2&animationin=None&animationout=None"
Start-Sleep -Seconds 0.2
Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/advanced?text=DOF2DMD&path=&size=M&color=ffffff&font=Matrix&bordercolor=33ffff&bordersize=0&cleanbg=true&duration=0.2&animationin=None&animationout=None"
Start-Sleep -Seconds 0.2
Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/advanced?text=DOF2DMD&path=&size=L&color=ffffff&font=Matrix&bordercolor=33ffff&bordersize=0&cleanbg=true&duration=0.2&animationin=None&animationout=None"
Start-Sleep -Seconds 0.2
#Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/advanced?text=DOF2DMD&path=&size=XL&color=ffffff&font=Matrix&bordercolor=33ffff&bordersize=0&cleanbg=true&duration=0.2&animationin=None&animationout=None"
#Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/picture?path=MatrixCode&duration=15&animation=Fade"

Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/advanced?text=DOF2DMD&path=&size=XL&color=ffffff&font=Matrix&bordercolor=33ffff&bordersize=0&cleanbg=true&duration=10&animationin=None&animationout=FadeOut"
