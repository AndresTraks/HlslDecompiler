vs_4_0
dcl_input_sgv v0.x, vertex_id
dcl_output_siv o0, position
dcl_output o1.xy
dcl_temps 1
ishl r0.x, v0.x, l(1)
and r0.x, r0.x, l(2)
and r0.z, v0.x, l(2)
utof r0.xy, r0.xz
mad o0.xy, r0.xy, l(2, -2, 0, 0), l(-1, 1, 0, 0)
mov o1.xy, r0.xy
mov o0.zw, l(0, 0, 0, 1)
ret
