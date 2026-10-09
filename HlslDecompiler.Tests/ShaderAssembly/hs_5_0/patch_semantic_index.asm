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
dcl_input v[3][1].xy
dcl_output o0.xyz
dcl_output o1.xy
dcl_temps 1
mov r0.x, vOutputControlPointID
mov o0.xyz, v[r0.x][0].xyz
add o1.xy, v[r0.x][1].xy, v[r0.x][1].xy
ret
hs_fork_phase
dcl_hs_fork_phase_instance_count 3
dcl_input vForkInstanceID
dcl_input vicp[3][0].xyz
dcl_output_siv o0.x, finalTriUeq0EdgeTessFactor
dcl_output_siv o1.x, finalTriVeq0EdgeTessFactor
dcl_output_siv o2.x, finalTriWeq0EdgeTessFactor
dcl_temps 1
dcl_indexrange o0 3
iadd r0.xy, vForkInstanceID.xx, l(2, 1, 0, 0)
udiv null, r0.xy, r0.xy, l(3, 3, 0, 0)
add r0.xyz, -vicp[r0.x][0].xyz, vicp[r0.y][0].xyz
dp3 r0.x, r0.xyz, r0.xyz
sqrt r0.x, r0.x
mul r0.x, r0.x, cb0[0].x
mov r0.y, vForkInstanceID.x
mov o[r0.y].x, r0.x
ret
hs_fork_phase
dcl_input vicp[3][0].x
dcl_output o0.y
dcl_temps 1
mad r0.x, vicp[0][0].x, l(2), vicp[1][0].x
mul o0.y, r0.x, l(0.333333343)
ret
hs_fork_phase
dcl_input vicp[3][0].y
dcl_output o0.z
dcl_temps 1
mad r0.x, vicp[0][0].y, l(2), vicp[1][0].y
mul o0.z, r0.x, l(0.333333343)
ret
hs_fork_phase
dcl_input vicp[3][0].z
dcl_output o0.w
dcl_temps 1
mad r0.x, vicp[0][0].z, l(2), vicp[1][0].z
mul o0.w, r0.x, l(0.333333343)
ret
hs_fork_phase
dcl_input vicp[3][0].x
dcl_output o1.y
dcl_temps 1
mad r0.x, vicp[1][0].x, l(2), vicp[0][0].x
mul o1.y, r0.x, l(0.333333343)
ret
hs_fork_phase
dcl_input vicp[3][0].y
dcl_output o1.z
dcl_temps 1
mad r0.x, vicp[1][0].y, l(2), vicp[0][0].y
mul o1.z, r0.x, l(0.333333343)
ret
hs_fork_phase
dcl_input vicp[3][0].z
dcl_output o1.w
dcl_temps 1
mad r0.x, vicp[1][0].z, l(2), vicp[0][0].z
mul o1.w, r0.x, l(0.333333343)
ret
hs_join_phase
dcl_input vpc0.x
dcl_input vpc1.x
dcl_input vpc2.x
dcl_output_siv o3.x, finalTriInsideTessFactor
dcl_temps 1
add r0.x, vpc0.x, vpc1.x
add r0.x, r0.x, vpc2.x
mul o3.x, r0.x, l(0.333333343)
ret
