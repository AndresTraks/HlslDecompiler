ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 1
div r0.x, v0.x, v0.y
lt r0.y, l(0), r0.x
lt r0.z, r0.x, l(0)
iadd r0.y, -r0.y, r0.z
itof o0.w, r0.y
ne r0.y, r0.x, r0.x
and r0.xz, r0.xx, l(2147483647, 0, 2139095040, 0)
and o0.x, r0.y, l(1065353216)
ieq r0.x, r0.x, l(2139095040)
ine r0.y, r0.z, l(2139095040)
and o0.yz, r0.xy, l(0, 1065353216, 1065353216, 0)
ret
