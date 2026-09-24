ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[2], immediateIndexed
dcl_output o0
dcl_temps 2
mov r0.xyz, cb0[0].xyz
mov r0.w, l(0)
loop
ige r1.x, r0.w, cb0[1].x
breakc_nz r1.x
mul r0.xyz, r0.yzx, l(1.5, 1.5, 1.5, 0)
iadd r0.w, r0.w, l(1)
endloop
mov o0.xyz, r0.xyz
mov o0.w, cb0[0].w
ret
