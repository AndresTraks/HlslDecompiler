ps_4_0
dcl_constantbuffer cb0[2], immediateIndexed
dcl_input_ps constant v0
dcl_input_ps constant v1.xy
dcl_output o0
dcl_temps 1
iadd r0.x, v0.z, -cb0[0].z
imax r0.x, -r0.x, r0.x
not r0.y, v0.w
and r0.y, r0.y, cb0[0].w
iadd r0.x, -r0.y, r0.x
itof o0.y, r0.x
umin r0.x, v1.y, cb0[1].x
imad r0.x, v1.x, cb0[1].x, r0.x
utof o0.z, r0.x
ilt r0.x, v0.x, v0.y
imin r0.y, v0.x, cb0[0].x
imax r0.z, v0.y, cb0[0].y
movc r0.x, r0.x, r0.y, r0.z
iadd r0.y, r0.z, r0.y
itof o0.xw, r0.yx
ret
