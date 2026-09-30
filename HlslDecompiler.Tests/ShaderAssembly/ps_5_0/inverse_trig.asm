ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps_siv linear noperspective v0.x, position
dcl_output o0
dcl_temps 2
min r0.xy, |v0.xx|, l(1, 2, 0, 0)
max r0.zw, |v0.xx|, l(0, 0, 1, 2)
div r0.xy, r0.xy, r0.zw
mul r0.zw, r0.xy, r0.xy
mad r1.xy, r0.zw, l(0.0208350997, 0.0208350997, 0, 0), l(-0.0851330012, -0.0851330012, 0, 0)
mad r1.xy, r0.zw, r1.xy, l(0.180141002, 0.180141002, 0, 0)
mad r1.xy, r0.zw, r1.xy, l(-0.330299497, -0.330299497, 0, 0)
mad r0.zw, r0.zw, r1.xy, l(0, 0, 0.999866009, 0.999866009)
mul r1.xy, r0.zw, r0.xy
mad r1.xy, r1.xy, l(-2, -2, 0, 0), l(1.57079637, 1.57079637, 0, 0)
lt r1.zw, l(0, 0, 1, 2), |v0.xx|
and r1.xy, r1.xy, r1.zw
mad r0.xy, r0.xy, r0.zw, r1.xy
min r0.zw, v0.xx, l(0, 0, 1, 2)
lt r0.zw, r0.zw, l(0, 0, 0, 0)
movc o0.xw, r0.zw, -r0.xy, r0.xy
max r0.x, v0.x, l(-1)
min r0.x, r0.x, l(1)
mad r0.y, |r0.x|, l(-0.0187292993), l(0.0742610022)
mad r0.y, r0.y, |r0.x|, l(-0.212114394)
mad r0.y, r0.y, |r0.x|, l(1.57072878)
add r0.z, -|r0.x|, l(1)
lt r0.x, r0.x, l(0)
sqrt r0.z, r0.z
mul r0.w, r0.z, r0.y
mad r0.w, r0.w, l(-2), l(3.14159274)
and r0.x, r0.w, r0.x
mad r0.x, r0.y, r0.z, r0.x
mad o0.yz, r0.xx, l(0, -1, 1, 0), l(0, 1.57079637, 0, 0)
ret
