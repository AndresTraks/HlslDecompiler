ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 2
mov r0, v0
mov r1.x, l(0)
loop
ige r1.y, r1.x, l(6)
breakc_nz r1.y
mad r0, r0, l(1.5, 1.5, 1.5, 1.5), -cb0[0]
lt r1.y, cb0[0].y, r0.x
if_nz r1.y
lt r1.y, r0.y, l(0)
if_nz r1.y
itof o0.z, r1.x
mov o0.w, l(1)
mov o0.xy, r0.zw
ret
endif
add r0.z, r0.z, l(1)
endif
iadd r1.x, r1.x, l(1)
endloop
mov o0, r0
ret
