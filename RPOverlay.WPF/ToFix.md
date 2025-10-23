Problemet är två saker: din extra kolumn reserverar bredd åt chattknappen och din chatt läggs ovanpå hela TabControl. Lösning: gör TabControl till enda kolumn. Flytta chattknappen in i TabControl-huvudet. Lägg chattlagret i content-raden i TabControl.Template och bind Visibility till knappen. Resultat: flikar syns alltid. Notes fyller full bredd. Chatt täcker bara ytan under flikarna.

Byt ut hela din yttre Grid mot detta:

```xml
<Grid Grid.Row="2" Margin="0,12,0,0">
    <TabControl x:Name="NotesTabControl"
                Background="Transparent"
                BorderThickness="0"
                Padding="0">

        <TabControl.Resources>
            <BooleanToVisibilityConverter x:Key="BoolToVis"/>

            <!-- Samma TabItem-style som du redan har -->
            <Style TargetType="TabItem">
                <Setter Property="Template">
                    <Setter.Value>
                        <ControlTemplate TargetType="TabItem">
                            <Border x:Name="Border"
                                    Background="#FF1C1C1C"
                                    BorderBrush="#FF2F9DFF"
                                    BorderThickness="1,1,1,0"
                                    CornerRadius="4,4,0,0"
                                    Padding="8,4"
                                    Margin="0,0,4,0">
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="Auto"/>
                                        <ColumnDefinition Width="Auto"/>
                                    </Grid.ColumnDefinitions>
                                    <ContentPresenter Grid.Column="0"
                                                      ContentSource="Header"
                                                      VerticalAlignment="Center"
                                                      HorizontalAlignment="Center"
                                                      Margin="0,0,8,0">
                                        <ContentPresenter.Resources>
                                            <Style TargetType="TextBlock">
                                                <Setter Property="FontSize" Value="11"/>
                                            </Style>
                                        </ContentPresenter.Resources>
                                    </ContentPresenter>
                                    <Button Grid.Column="1"
                                            Content="✕"
                                            Click="CloseTab_Click"
                                            Tag="{Binding RelativeSource={RelativeSource TemplatedParent}}"
                                            Width="14"
                                            Height="14"
                                            FontSize="8"
                                            Background="Transparent"
                                            Foreground="#FFB0B0B0"
                                            BorderThickness="0"
                                            Cursor="Hand"
                                            VerticalAlignment="Center"
                                            Padding="0"
                                            ToolTip="Stäng flik"/>
                                </Grid>
                            </Border>
                            <ControlTemplate.Triggers>
                                <Trigger Property="IsSelected" Value="True">
                                    <Setter TargetName="Border" Property="Background" Value="#FF2F9DFF"/>
                                </Trigger>
                                <Trigger Property="IsSelected" Value="False">
                                    <Setter TargetName="Border" Property="Background" Value="#FF2A2A2A"/>
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
                <Setter Property="Foreground" Value="White"/>
            </Style>
        </TabControl.Resources>

        <TabControl.Template>
            <ControlTemplate TargetType="TabControl">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>

                    <!-- Huvud: flikar vänster. +. Chattknapp längst till höger -->
                    <DockPanel Grid.Row="0">
                        <!-- Chattknapp till höger -->
                        <ToggleButton x:Name="ChatToggle"
                                      DockPanel.Dock="Right"
                                      Content="💬 Chatt"
                                      Height="24"
                                      Foreground="White"
                                      BorderThickness="0"
                                      FontSize="11"
                                      Cursor="Hand"
                                      Padding="8,4"
                                      Margin="4,0,0,0"
                                      VerticalAlignment="Top"
                                      ToolTip="Öppna AI Chat">
                            <ToggleButton.Template>
                                <ControlTemplate TargetType="ToggleButton">
                                    <Border Background="{TemplateBinding Background}"
                                            BorderBrush="#FF2F9DFF"
                                            BorderThickness="1,1,1,0"
                                            CornerRadius="4,4,0,0"
                                            Padding="{TemplateBinding Padding}">
                                        <ContentPresenter HorizontalAlignment="Center" 
                                                          VerticalAlignment="Center"/>
                                    </Border>
                                </ControlTemplate>
                            </ToggleButton.Template>
                            <ToggleButton.Style>
                                <Style TargetType="ToggleButton">
                                    <Setter Property="Background" Value="#FF2A2A2A"/>
                                    <Style.Triggers>
                                        <Trigger Property="IsChecked" Value="True">
                                            <Setter Property="Background" Value="#FF2F9DFF"/>
                                        </Trigger>
                                    </Style.Triggers>
                                </Style>
                            </ToggleButton.Style>
                        </ToggleButton>

                        <!-- + knapp till höger om flikar men vänster om chatt -->
                        <Button DockPanel.Dock="Right"
                                Content="+"
                                Click="AddNoteTab_Click"
                                Width="24"
                                Height="24"
                                Background="#FF2F9DFF"
                                Foreground="White"
                                BorderThickness="0"
                                FontSize="16"
                                FontWeight="Bold"
                                Cursor="Hand"
                                Margin="4,0,0,0"
                                VerticalAlignment="Top"
                                ToolTip="Lägg till ny anteckning"/>

                        <!-- Flikhuvuden fyller resten -->
                        <TabPanel IsItemsHost="True" Background="Transparent"/>
                    </DockPanel>

                    <!-- Innehåll: notes och chattoverlay i samma cell -->
                    <Grid Grid.Row="1">
                        <!-- Notes-innehåll -->
                        <ScrollViewer VerticalScrollBarVisibility="Auto"
                                      Background="Transparent">
                            <ScrollViewer.Resources>
                                <Style TargetType="{x:Type ScrollBar}">
                                    <Setter Property="Width" Value="4"/>
                                    <Setter Property="MinWidth" Value="4"/>
                                    <Setter Property="Background" Value="Transparent"/>
                                    <Setter Property="Foreground" Value="#FF2F9DFF"/>
                                    <Setter Property="Template">
                                        <Setter.Value>
                                            <ControlTemplate TargetType="{x:Type ScrollBar}">
                                                <Grid x:Name="Bg" Width="4">
                                                    <Track x:Name="PART_Track" 
                                                           IsDirectionReversed="True"
                                                           Width="4">
                                                        <Track.DecreaseRepeatButton>
                                                            <RepeatButton Opacity="0" Command="ScrollBar.PageUpCommand"/>
                                                        </Track.DecreaseRepeatButton>
                                                        <Track.Thumb>
                                                            <Thumb Width="4">
                                                                <Thumb.Template>
                                                                    <ControlTemplate TargetType="{x:Type Thumb}">
                                                                        <Border Background="#FF2F9DFF" 
                                                                                CornerRadius="2"
                                                                                Width="4"
                                                                                Opacity="0.6"/>
                                                                    </ControlTemplate>
                                                                </Thumb.Template>
                                                            </Thumb>
                                                        </Track.Thumb>
                                                        <Track.IncreaseRepeatButton>
                                                            <RepeatButton Opacity="0" Command="ScrollBar.PageDownCommand"/>
                                                        </Track.IncreaseRepeatButton>
                                                    </Track>
                                                </Grid>
                                            </ControlTemplate>
                                        </Setter.Value>
                                    </Setter>
                                </Style>
                            </ScrollViewer.Resources>

                            <ContentPresenter ContentSource="SelectedContent"
                                              HorizontalAlignment="Stretch"
                                              VerticalAlignment="Top"
                                              Margin="0,4,0,0"/>
                        </ScrollViewer>

                        <!-- Chattoverlay: syns bara när ChatToggle är vald -->
                        <Grid x:Name="ChatInterface"
                              Visibility="{Binding IsChecked, ElementName=ChatToggle, Converter={StaticResource BoolToVis}}"
                              Background="#FF1E1E1E"
                              Panel.ZIndex="1">
                            <Grid.RowDefinitions>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="*"/>
                                <RowDefinition Height="Auto"/>
                            </Grid.RowDefinitions>

                            <!-- Chatt-topp -->
                            <Border Grid.Row="0" 
                                    Background="#FF2A2A2A" 
                                    Padding="8,4"
                                    BorderBrush="#FF3A3A3A"
                                    BorderThickness="0,0,0,1">
                                <Grid>
                                    <TextBlock Text="AI Chatt"
                                               FontSize="14"
                                               FontWeight="SemiBold"
                                               Foreground="White"
                                               VerticalAlignment="Center"
                                               HorizontalAlignment="Left"/>
                                    <Button Content="✕ Stäng"
                                            Click="CloseChat_Click"
                                            Background="Transparent"
                                            Foreground="White"
                                            BorderThickness="0"
                                            FontSize="12"
                                            Padding="8,4"
                                            Cursor="Hand"
                                            HorizontalAlignment="Right"
                                            ToolTip="Stäng chattläge"/>
                                </Grid>
                            </Border>

                            <!-- Meddelanden -->
                            <ScrollViewer Grid.Row="1"
                                          x:Name="ChatScrollViewer"
                                          VerticalScrollBarVisibility="Auto"
                                          Background="Transparent">
                                <ScrollViewer.Resources>
                                    <Style TargetType="{x:Type ScrollBar}">
                                        <Setter Property="Width" Value="4"/>
                                        <Setter Property="MinWidth" Value="4"/>
                                        <Setter Property="Background" Value="Transparent"/>
                                        <Setter Property="Foreground" Value="#FF2F9DFF"/>
                                        <Setter Property="Template">
                                            <Setter.Value>
                                                <ControlTemplate TargetType="{x:Type ScrollBar}">
                                                    <Grid x:Name="Bg" Width="4">
                                                        <Track x:Name="PART_Track" 
                                                               IsDirectionReversed="True"
                                                               Width="4">
                                                            <Track.DecreaseRepeatButton>
                                                                <RepeatButton Opacity="0" Command="ScrollBar.PageUpCommand"/>
                                                            </Track.DecreaseRepeatButton>
                                                            <Track.Thumb>
                                                                <Thumb Width="4">
                                                                    <Thumb.Template>
                                                                        <ControlTemplate TargetType="{x:Type Thumb}">
                                                                            <Border Background="#FF2F9DFF" 
                                                                                    CornerRadius="2"
                                                                                    Width="4"
                                                                                    Opacity="0.6"/>
                                                                        </ControlTemplate>
                                                                    </Thumb.Template>
                                                                </Thumb>
                                                            </Track.Thumb>
                                                            <Track.IncreaseRepeatButton>
                                                                <RepeatButton Opacity="0" Command="ScrollBar.PageDownCommand"/>
                                                            </Track.IncreaseRepeatButton>
                                                        </Track>
                                                    </Grid>
                                                </ControlTemplate>
                                            </Setter.Value>
                                        </Setter>
                                    </Style>
                                </ScrollViewer.Resources>
                                <Border Padding="8">
                                    <ItemsControl x:Name="ChatMessages">
                                        <ItemsControl.ItemTemplate>
                                            <DataTemplate>
                                                <Border Margin="4,2"
                                                        Padding="10,8"
                                                        CornerRadius="8"
                                                        HorizontalAlignment="Stretch">
                                                    <Border.Style>
                                                        <Style TargetType="Border">
                                                            <Setter Property="Background" Value="#FF2F9DFF"/>
                                                            <Style.Triggers>
                                                                <DataTrigger Binding="{Binding IsUser}" Value="False">
                                                                    <Setter Property="Background" Value="#FF2A2A2A"/>
                                                                </DataTrigger>
                                                            </Style.Triggers>
                                                        </Style>
                                                    </Border.Style>
                                                    <TextBox Text="{Binding Content, Mode=OneWay}"
                                                             TextWrapping="Wrap"
                                                             Foreground="White"
                                                             FontSize="{Binding FontSize}"
                                                             Background="Transparent"
                                                             BorderThickness="0"
                                                             IsReadOnly="True"
                                                             Cursor="Arrow"
                                                             Focusable="True"
                                                             IsTabStop="False"/>
                                                </Border>
                                            </DataTemplate>
                                        </ItemsControl.ItemTemplate>
                                    </ItemsControl>
                                </Border>
                            </ScrollViewer>

                            <!-- Input -->
                            <Border Grid.Row="2" Background="#FF2A2A2A" Padding="8">
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="*"/>
                                        <ColumnDefinition Width="Auto"/>
                                    </Grid.ColumnDefinitions>

                                    <TextBox x:Name="ChatInputBox"
                                             Grid.Column="0"
                                             Background="#FF1C1C1C"
                                             Foreground="White"
                                             BorderBrush="#FF3A3A3A"
                                             BorderThickness="1"
                                             Padding="8"
                                             FontSize="13"
                                             TextWrapping="Wrap"
                                             AcceptsReturn="False"
                                             MaxHeight="100"
                                             VerticalScrollBarVisibility="Auto"
                                             KeyDown="ChatInput_KeyDown"
                                             Text="{Binding ChatInputText, UpdateSourceTrigger=PropertyChanged}"/>

                                    <Button x:Name="SendButton"
                                            Grid.Column="1"
                                            Click="SendChat_Click"
                                            Width="40"
                                            Height="40"
                                            Margin="4,0,0,0"
                                            Cursor="Hand"
                                            IsEnabled="{Binding CanSendMessage}"
                                            ToolTip="Skicka meddelande (Enter)">
                                        <Button.Template>
                                            <ControlTemplate TargetType="Button">
                                                <Border x:Name="SendButtonBorder"
                                                        Background="#FF2F9DFF"
                                                        CornerRadius="4"
                                                        BorderBrush="#FF1E9FFF"
                                                        BorderThickness="1">
                                                    <ContentPresenter HorizontalAlignment="Center"
                                                                      VerticalAlignment="Center"/>
                                                </Border>
                                                <ControlTemplate.Triggers>
                                                    <Trigger Property="IsMouseOver" Value="True">
                                                        <Setter TargetName="SendButtonBorder" Property="Background" Value="#FF1E9FFF"/>
                                                    </Trigger>
                                                    <Trigger Property="IsPressed" Value="True">
                                                        <Setter TargetName="SendButtonBorder" Property="Background" Value="#FF0D8FFF"/>
                                                    </Trigger>
                                                    <Trigger Property="IsEnabled" Value="False">
                                                        <Setter TargetName="SendButtonBorder" Property="Background" Value="#FF555555"/>
                                                        <Setter Property="Foreground" Value="#FF888888"/>
                                                    </Trigger>
                                                </ControlTemplate.Triggers>
                                            </ControlTemplate>
                                        </Button.Template>
                                        <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" VerticalAlignment="Center">
                                            <TextBlock Text="➤" FontSize="14" Foreground="White" VerticalAlignment="Center"/>
                                        </StackPanel>
                                    </Button>
                                </Grid>
                            </Border>
                        </Grid>
                    </Grid>
                </Grid>
            </ControlTemplate>
        </TabControl.Template>
    </TabControl>
</Grid>
```

Kod bakom som behövs:

```csharp
// Stäng-knappen i chattens topp
private void CloseChat_Click(object sender, RoutedEventArgs e)
{
    var toggle = NotesTabControl.Template.FindName("ChatToggle", NotesTabControl) as ToggleButton;
    if (toggle != null) toggle.IsChecked = false;
}

// Ta bort allt som sätter ChatInterface.Visibility manuellt
// ChatToggle_Click kan raderas eller lämnas tom
```

Om du i kod bakom tidigare gick mot ChatMessages, ChatInputBox eller ChatScrollViewer via fält: hämta dem via templaten när du behöver dem.

```csharp
var chatMessages = NotesTabControl.Template.FindName("ChatMessages", NotesTabControl) as ItemsControl;
var chatInput    = NotesTabControl.Template.FindName("ChatInputBox", NotesTabControl) as TextBox;
var chatScroll   = NotesTabControl.Template.FindName("ChatScrollViewer", NotesTabControl) as ScrollViewer;
```

Varför det funkar:
1: Ingen extra kolumn som tar bredd.
2: Chatt är ett overlay i content-raden. Flikhuvuden ligger i sin egen rad.
3: Visibility binds till chattknappen. Inga manuella pixelmått.