ps_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 1
switch cb0[0].x
case l(0)
switch cb0[0].y
case l(1)
mov r0, v0
break
default
mov r0, -v0
break
endswitch
break
case l(2)
add r0, v0, v0
break
default
add r0, v0, l(1, 1, 1, 1)
break
endswitch
mov o0, r0
ret
