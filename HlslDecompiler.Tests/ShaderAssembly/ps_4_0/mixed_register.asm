ps_4_0
dcl_constantbuffer cb0[2], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 2
mov r0.xyz, l(0, 0, 0, 0)
loop
ige r0.w, r0.z, cb0[1].x
breakc_nz r0.w
itof r0.w, r0.z
mad r1.xy, cb0[0].xy, r0.ww, v0.xy
sample_l r1, r1.xyxx, t0, s0, l(0)
lt r0.w, cb0[0].z, r1.x
add r1.x, r0.x, r1.x
iadd r1.y, r0.y, l(1)
movc r0.xy, r0.ww, r1.xy, r0.xy
iadd r0.z, r0.z, l(1)
endloop
itof r1.y, r0.y
max r0.y, r1.y, l(1)
div r1.x, r0.x, r0.y
mov_sat o0.z, r1.x
mov o0.w, cb0[0].w
mov o0.xy, r1.xy
ret
