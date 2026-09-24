ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_output o0
dcl_temps 1
add r0.x, cb0[0].y, l(2)
rcp o0.y, r0.x
exp o0.z, cb0[0].z
add r0.xy, |cb0[0].xw|, l(1, 1, 0, 0)
rsq o0.x, r0.x
log o0.w, r0.y
ret
