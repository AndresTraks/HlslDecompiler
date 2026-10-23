ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps linear v1.xyz
dcl_output o0
dcl_temps 2
dp3 r0.x, v1.xyz, v1.xyz
sqrt r0.x, r0.x
mov r0.yz, l(0, 0, 0, 0)
loop
ige r0.w, r0.y, cb0[0].x
breakc_nz r0.w
mad r0.w, r0.x, l(0.100000001), r0.z
lt r1.x, cb0[0].y, r0.w
if_nz r1.x
mov r0.z, r0.w
break
endif
iadd r0.y, r0.y, l(1)
mov r0.yz, r0.yw
endloop
itof r0.x, r0.y
itof r0.y, cb0[0].x
div r0.x, r0.x, r0.y
add r0.x, -r0.x, l(1)
mul o0.xyz, r0.zzz, r0.xxx
mov o0.w, l(1)
ret
