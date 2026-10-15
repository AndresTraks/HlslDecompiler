ps_4_0
dcl_input_ps linear v0
dcl_output o0
dcl_temps 1
ushr r0.x, v0.w, l(30)
switch r0.x
case l(0)
mov r0, v0
break
case l(1)
add r0, v0, v0
break
case l(2)
xor r0, v0, l(-2147483648, -2147483648, -2147483648, -2147483648)
break
default
mov r0, l(1, 1, 1, 1)
break
endswitch
mov o0, r0
ret
