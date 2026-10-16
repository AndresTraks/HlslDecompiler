ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps constant v0.x
dcl_input_ps linear v1
dcl_output o0
dcl_temps 1
and r0.x, v0.x, l(7)
switch r0.x
case l(0)
mov r0.x, l(1056964608)
break
case l(1)
mov r0.x, l(1069547520)
break
case l(2)
mov r0.x, l(-1073741824)
break
case l(3)
mov r0.x, l(1088421888)
break
default
mov r0.x, l(0)
break
endswitch
mul o0, r0.x, v1
ret
