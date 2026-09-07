ps_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_output o0
dcl_temps 1
round_z o0.w, cb0[0].y
ftoi r0.x, cb0[0].y
ftou r0.y, cb0[0].x
iadd r0.x, r0.x, r0.y
utof o0.xz, r0.xy
ishl r0.x, r0.y, l(1)
utof o0.y, r0.x
ret
