ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0
dcl_input_ps constant v1.x
dcl_output o0
dcl_temps 4
and r0.x, v1.x, l(3)
ubfe r0.y, l(2), l(2), v1.x
mov r1, l(0, 0, 0, 0)
mov r0.z, l(0)
loop
uge r0.w, r0.z, r0.x
breakc_nz r0.w
mov r2, r1
mov r0.w, r0.z
mov r3.x, l(0)
loop
uge r3.y, r3.x, r0.y
breakc_nz r3.y
ieq r3.y, r0.w, r3.x
if_nz r3.y
break
endif
iadd r3.y, r0.w, r3.x
utof r3.y, r3.y
mad r2, v0, r3.y, r2
iadd r3.x, r3.x, l(1)
endloop
mov r1.xyz, r2.xyz
add r1.w, r2.w, l(1)
iadd r0.z, r0.z, l(1)
endloop
mov o0, r1
ret
