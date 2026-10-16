ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0
dcl_input_ps constant v1.x
dcl_output o0
dcl_temps 3
and r0.x, v1.x, l(3)
if_z r0.x
mov r1, v0
else
ieq r0.y, r0.x, l(1)
if_nz r0.y
mov r1, v0.yzwx
else
mov r1, -v0
endif
endif
mov r2, r1
mov r0.y, l(0)
loop
uge r0.z, r0.y, r0.x
breakc_nz r0.z
add r2, r2, l(1, 1, 1, 1)
iadd r0.y, r0.y, l(1)
endloop
mov o0, r2
ret
