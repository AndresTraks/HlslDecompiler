ps_5_0
dcl_globalFlags refactoringAllowed
dcl_immediateConstantBuffer { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { -1, 0, 0, 0 }, { 0, -1, 0, 0 }, { 0.5, 0.5, 0.5, 0 }, { -0.5, 0.5, 0.5, 0 }, { 0.5, -0.5, 0.5, 0 }, { -0.5, -0.5, 0.5, 0 }, { 0.699999988, 0, 0.300000012, 0 }, { -0.699999988, 0, 0.300000012, 0 }, { 0, 0.699999988, 0.300000012, 0 }, { 0, -0.699999988, 0.300000012, 0 } }
dcl_constantbuffer CB0[6], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_input_ps_siv linear noperspective v0.x, position
dcl_input_ps linear v1.xy
dcl_output o0
dcl_temps 3
sample_l_indexable(texture2d)(float,float,float,float) r0.x, v1.x, t0.x, s0, l(0)
sample_l_indexable(texture2d)(float,float,float,float) r0.yzw, v1.yxx, t1.xyz, s0, l(0)
mad r0.yzw, r0.yzw, l(0, 2, 2, 2), l(0, -1, -1, -1)
ftou r1.x, v0.x
and r1.x, r1.x, l(3)
mov r1.yz, l(0, 0, 0, 0)
loop
uge r1.w, r1.z, cb0[5].x
breakc_nz r1.w
and r1.w, r1.z, l(7)
mul r2.x, icb[r1.x].y, icb[r1.w + 4].y
mad r2.x, icb[r1.w + 4].x, icb[r1.x].x, -r2.x
dp2 r2.y, icb[r1.w + 4].yx, icb[r1.x].xy
mul r2.xy, r2.xy, cb0[4].zz
mad r2.xy, r2.xy, cb0[4].xy, v1.xy
sample_l_indexable(texture2d)(float,float,float,float) r2.x, r2.x, t0.x, s0, l(0)
add r2.x, r0.x, -r2.x
lt r2.y, cb0[4].w, r2.x
lt r2.x, r2.x, cb0[4].z
and r2.x, r2.x, r2.y
dp3_sat r1.w, r0.yzw, icb[r1.w + 4].xyz
and r1.w, r1.w, r2.x
add r1.y, r1.w, r1.y
iadd r1.z, r1.z, l(1)
endloop
utof r0.x, cb0[5].x
div r0.x, r1.y, r0.x
add o0, -r0.x, l(1, 1, 1, 1)
ret
