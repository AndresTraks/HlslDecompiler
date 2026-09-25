ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0.xz
dcl_output o0
dcl_temps 2
lt r0.x, l(1), v0.x
mov r0.yz, l(0, 0, 0, 0)
loop
ige r0.w, r0.z, l(8)
breakc_nz r0.w
movc r0.w, r0.x, l(8), r0.z
lt r1.x, r0.y, l(-100)
if_nz r1.x
iadd r0.z, r0.w, l(1)
continue
endif
add r0.y, r0.y, v0.z
iadd r0.z, r0.w, l(1)
endloop
mov o0, r0.y
ret
