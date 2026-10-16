ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0
dcl_input_ps constant v1.x
dcl_output o0
dcl_temps 3
if_z v1.x
mov o0, v0
ret
endif
ieq r0.x, v1.x, l(1)
if_nz r0.x
mov r0, v0.xzwy
else
ieq r1.x, v1.x, l(2)
if_nz r1.x
mov o0, l(1, 1, 1, 1)
ret
endif
mov r0, -v0.yzwx
endif
and r1.x, v1.x, l(3)
mov r2, r0.wxyz
mov r1.y, l(0)
loop
uge r1.z, r1.y, r1.x
breakc_nz r1.z
utof r1.z, r1.y
lt r1.w, r1.z, r2.x
if_nz r1.w
mul o0, r1.z, r2
ret
endif
add r2, r2, l(1, 1, 1, 1)
iadd r1.y, r1.y, l(1)
endloop
mov o0, r2
ret
