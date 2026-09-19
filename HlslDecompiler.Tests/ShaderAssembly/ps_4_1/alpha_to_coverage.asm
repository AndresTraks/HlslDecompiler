ps_4_1
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0
dcl_output o0
dcl_output oMask
dcl_temps 1
mul r0.x, v0.w, l(4)
ftou r0.x, r0.x
mov r0.yz, l(0, 0, 0, 0)
loop
uge r0.w, r0.z, r0.x
breakc_nz r0.w
ishl r0.w, l(1), r0.z
or r0.y, r0.w, r0.y
iadd r0.z, r0.z, l(1)
endloop
mov oMask, r0.y
mov o0.xyz, v0.xyz
mov o0.w, l(1)
ret
