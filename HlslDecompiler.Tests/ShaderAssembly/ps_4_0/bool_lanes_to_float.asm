ps_4_0
dcl_input_ps constant v0
dcl_input_ps linear v1
dcl_output o0
dcl_temps 3
or r0.xy, v0.zw, v0.xy
or r0.x, r0.y, r0.x
ine r0.x, r0.x, l(0)
lt r0.y, v1.y, v1.x
lt r0.z, v1.z, l(0)
and r0.y, r0.z, r0.y
or r0.y, r0.y, r0.x
ieq r1, v0.yzwx, v0
and r1.xy, r1.zw, r1.xy
and r0.z, r1.y, r1.x
ilt r1, l(1, 1, 1, 1), v0
and r2.x, r1.w, r1.z
not r2.x, r2.x
or r2.y, r1.y, r1.x
and r0.x, r2.x, r2.y
lt r2, l(0, 0, 0, 0), v1
ine r1, r1, r2
or r1.xy, r1.zw, r1.xy
or r0.w, r1.y, r1.x
and o0, r0, l(1065353216, 1065353216, 1065353216, 1065353216)
ret
