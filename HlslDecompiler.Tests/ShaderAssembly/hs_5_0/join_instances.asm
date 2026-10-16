hs_5_0
hs_decls
dcl_input_control_point_count 4
dcl_output_control_point_count 4
dcl_tessellator_domain domain_quad
dcl_tessellator_partitioning partitioning_fractional_even
dcl_tessellator_output_primitive output_triangle_ccw
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
hs_control_point_phase
dcl_input vOutputControlPointID
dcl_input v[4][0]
dcl_input v[4][1].xyz
dcl_output o0
dcl_output o1.xyz
dcl_temps 1
mov r0.x, vOutputControlPointID
mov o0, v[r0.x][0]
dp3 r0.y, v[r0.x][1].xyz, v[r0.x][1].xyz
rsq r0.y, r0.y
mul o1.xyz, r0.yyy, v[r0.x][1].xyz
ret
hs_fork_phase
dcl_hs_fork_phase_instance_count 4
dcl_input vForkInstanceID
dcl_input vicp[4][0].xyz
dcl_output_siv o0.x, finalQuadUeq0EdgeTessFactor
dcl_output_siv o1.x, finalQuadVeq0EdgeTessFactor
dcl_output_siv o2.x, finalQuadUeq1EdgeTessFactor
dcl_output_siv o3.x, finalQuadVeq1EdgeTessFactor
dcl_temps 1
dcl_indexrange o0 4
iadd r0.x, vForkInstanceID.x, l(1)
and r0.x, r0.x, l(3)
mov r0.y, vForkInstanceID.x
add r0.xzw, -vicp[r0.x][0].xyz, vicp[r0.y][0].xyz
dp3 r0.x, r0.xzw, r0.xzw
sqrt r0.x, r0.x
mul r0.x, r0.x, cb0[0].x
mov o[r0.y].x, r0.x
ret
hs_fork_phase
dcl_input vicp[4][0].x
dcl_output o0.y
dcl_temps 1
add r0.x, vicp[1][0].x, vicp[0][0].x
add r0.x, r0.x, vicp[2][0].x
add r0.x, r0.x, vicp[3][0].x
mul o0.y, r0.x, l(0.25)
ret
hs_fork_phase
dcl_input vicp[4][0].y
dcl_output o0.z
dcl_temps 1
add r0.x, vicp[1][0].y, vicp[0][0].y
add r0.x, r0.x, vicp[2][0].y
add r0.x, r0.x, vicp[3][0].y
mul o0.z, r0.x, l(0.25)
ret
hs_fork_phase
dcl_input vicp[4][0].z
dcl_output o0.w
dcl_temps 1
add r0.x, vicp[1][0].z, vicp[0][0].z
add r0.x, r0.x, vicp[2][0].z
add r0.x, r0.x, vicp[3][0].z
mul o0.w, r0.x, l(0.25)
ret
hs_join_phase
dcl_hs_join_phase_instance_count 2
dcl_input vpc0.x
dcl_input vpc1.x
dcl_input vpc2.x
dcl_input vpc3.x
dcl_input vJoinInstanceID
dcl_output_siv o4.x, finalQuadUInsideTessFactor
dcl_output_siv o5.x, finalQuadVInsideTessFactor
dcl_temps 1
dcl_indexrange o4 2
dcl_indexrange vpc0 4
mov r0.x, vJoinInstanceID.x
max r0.y, vpc[r0.x].x, vpc[r0.x + 2].x
mov o[r0.x + 4].x, r0.y
ret
