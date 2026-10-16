ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0
dcl_input_ps constant v1.x
dcl_output o0
dcl_temps 2
and r0.x, v1.x, l(1)
imul null, r0.y, v1.x, l(3)
ult r0.z, l(7), r0.y
movc r1.xyz, r0.zzz, v0.xyz, v0.wzy
movc o0.x, r0.x, r0.y, r1.x
mov o0.yz, r1.yz
utof o0.w, r0.y
ret
