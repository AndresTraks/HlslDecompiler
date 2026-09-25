hs_5_0
hs_decls
dcl_input_control_point_count 3
dcl_output_control_point_count 6
dcl_tessellator_domain domain_tri
dcl_tessellator_partitioning partitioning_fractional_odd
dcl_tessellator_output_primitive output_triangle_cw
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[2], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
hs_control_point_phase
dcl_input vOutputControlPointID
dcl_input v[3][0].xyz
dcl_input v[3][1].xyz
dcl_output o0.xyz
dcl_output o1.xyz
dcl_temps 2
ult r0.x, vOutputControlPointID, l(3)
if_nz r0.x
mov r0.x, vOutputControlPointID
mov o0.xyz, v[r0.x][0].xyz
mov o1.xyz, v[r0.x][1].xyz
else
iadd r0.xy, vOutputControlPointID, l(-3, -2, 0, 0)
udiv null, r0.y, r0.y, l(3)
add r1.xyz, v[r0.y][0].xyz, v[r0.x][0].xyz
mul o0.xyz, r1.xyz, l(0.5, 0.5, 0.5, 0)
add r0.xyz, v[r0.y][1].xyz, v[r0.x][1].xyz
dp3 r0.w, r0.xyz, r0.xyz
rsq r0.w, r0.w
mul o1.xyz, r0.www, r0.xyz
endif
ret
hs_join_phase
dcl_input vPrim
dcl_input vicp[3][0].xyz
dcl_output_siv o0.x, finalTriUeq0EdgeTessFactor
dcl_output_siv o1.x, finalTriVeq0EdgeTessFactor
dcl_output_siv o2.x, finalTriWeq0EdgeTessFactor
dcl_output_siv o3.x, finalTriInsideTessFactor
dcl_temps 2
add r0.xyz, vicp[1][0].xyz, vicp[0][0].xyz
add r0.xyz, r0.xyz, vicp[2][0].xyz
mul r1.xy, r0.xz, l(0.00333333341, 0.00333333341, 0, 0)
mad r0.xyz, r0.xyz, l(0.333333343, 0.333333343, 0.333333343, 0), -cb0[0].xyz
dp3 r0.x, r0.xyz, r0.xyz
sqrt r0.x, r0.x
max r0.x, r0.x, l(1)
sample_l_indexable(texture2d)(float,float,float,float) r0.y, r1.y, t0.x, s0, l(0)
add r0.z, -cb0[1].x, cb0[1].y
mad r0.y, r0.y, r0.z, cb0[1].x
mul r0.y, r0.y, cb0[0].w
div r0.x, r0.y, r0.x
mov o0.x, r0.x
mov o1.x, r0.x
mov o2.x, r0.x
and r0.y, vPrim, l(1)
utof r0.y, r0.y
add o3.x, r0.y, r0.x
ret
