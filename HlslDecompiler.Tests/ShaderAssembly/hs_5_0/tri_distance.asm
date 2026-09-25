hs_5_0
hs_decls
dcl_input_control_point_count 3
dcl_output_control_point_count 3
dcl_tessellator_domain domain_tri
dcl_tessellator_partitioning partitioning_fractional_odd
dcl_tessellator_output_primitive output_triangle_cw
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
hs_control_point_phase
dcl_input vOutputControlPointID
dcl_input v[3][0].xyz
dcl_input v[3][1].xyz
dcl_output o0.xyz
dcl_output o1.xyz
dcl_temps 1
mov r0.x, vOutputControlPointID
mov o0.xyz, v[r0.x][0].xyz
dp3 r0.y, v[r0.x][1].xyz, v[r0.x][1].xyz
rsq r0.y, r0.y
mul o1.xyz, r0.yyy, v[r0.x][1].xyz
ret
hs_join_phase
dcl_input vicp[3][0].xyz
dcl_output_siv o0.x, finalTriUeq0EdgeTessFactor
dcl_output_siv o1.x, finalTriVeq0EdgeTessFactor
dcl_output_siv o2.x, finalTriWeq0EdgeTessFactor
dcl_output_siv o3.x, finalTriInsideTessFactor
dcl_temps 1
add r0.xyz, vicp[1][0].xyz, vicp[0][0].xyz
add r0.xyz, r0.xyz, vicp[2][0].xyz
mad r0.xyz, r0.xyz, l(0.333333343, 0.333333343, 0.333333343, 0), -cb0[0].xyz
dp3 r0.x, r0.xyz, r0.xyz
sqrt r0.x, r0.x
div r0.x, cb0[0].w, r0.x
max r0.x, r0.x, l(1)
min r0.x, r0.x, l(16)
mov o0.x, r0.x
mov o1.x, r0.x
mov o2.x, r0.x
mov o3.x, r0.x
ret
