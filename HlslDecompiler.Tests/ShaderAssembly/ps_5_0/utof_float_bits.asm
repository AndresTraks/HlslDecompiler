ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 1
itof o0.y, v0.y
mov o0.w, l(1)
utof r0.xy, cb0[0].xz
mul o0.z, r0.y, v0.x
mov o0.x, r0.x
ret
